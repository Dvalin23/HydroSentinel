# PostgreSQL deployment database

HydroSentinel uses SQLite with `EnsureCreated` only in Development. Production requires PostgreSQL and the checked-in EF Core migration under `Data/Migrations/Postgres`. Production startup rejects SQLite, requires a connection string from the host's secret store, and refuses to start while migrations are pending. It does not create or alter the production schema at runtime.

## Provisioning and release steps

1. Provision a PostgreSQL database with automated backups and TLS enabled. Create a dedicated application database and separate migration/runtime credentials when the host supports that setup.
2. Configure these environment values in the hosting platform:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `Database__Provider=Postgres`
   - `ConnectionStrings__Default` set in the platform's secret store. Use the provider's TLS-verified connection string, for example with `SSL Mode=VerifyFull` and the required CA certificate.
3. Run `deployment/migrate-postgres.ps1` as a one-time/release migration step before starting or rolling out the app. Do not run schema changes concurrently from multiple application instances.
4. Start the application with a runtime database account limited to the permissions the app needs. Use the migration credential only for the migration step.
5. Confirm that startup succeeds, then configure backups and practice restoring one before storing production customer or payment data.

`deployment/postgres-schema.sql` is an idempotent PostgreSQL script generated from the checked-in migrations. Regenerate it after each migration with:

```powershell
dotnet tool run dotnet-ef migrations script --idempotent --context AppDbContext --output deployment/postgres-schema.sql
```

The repository-local EF tool version is pinned in `.config/dotnet-tools.json`. For local development, leave `Database:Provider` as SQLite; the PostgreSQL migration is intended for production and should not be applied to the existing `hydrosentinel.db` file.

## Render deployment

The repository includes `Dockerfile` and `render.yaml` for a Render web service and managed PostgreSQL database in Frankfurt. The Blueprint uses paid service/database plans because Render's free services sleep, have ephemeral storage, and are not suitable for persistent production accounts or recurring billing. Review current Render pricing before creating resources; syncing the Blueprint provisions billable services.

1. Push this repository to GitHub and choose **New + → Blueprint** in Render, then connect the repository and select `render.yaml`.
2. Review the service and database plan/region before confirming. Keep the app and database in the same region so the app uses Render's private database connection.
3. Supply the prompted credentials. Use a unique strong admin seed password, Gmail SMTP app password (or another SMTP provider), Paystack secret key and plan codes, and a long random relay secret. Never commit these values.
4. The app uses Render's HTTPS `RENDER_EXTERNAL_URL` for email and Paystack callbacks by default. If you attach a custom domain, set `Email__PublicBaseUrl` and `Paystack__PublicBaseUrl` in the service environment to that final HTTPS origin. Keep Paystack on test credentials until production terms and account verification are complete.
5. Deploy. Render builds the Docker image, runs the bundled EF migration as a pre-deploy step, then starts the app. Startup checks that the database is reachable and no migrations are pending; `/healthz` is Render's health check.
6. Confirm registration email, login, password reset, admin login, Paystack test checkout/webhook, and device relay check-in on the hosted HTTPS URL before pointing real customers/devices at it.

The app and migration bundle both convert Render's `postgresql://` database URL to Npgsql's connection-string format. The Blueprint blocks public database ingress (`ipAllowList: []`); the web service connects over Render's private network. Data Protection keys are stored in PostgreSQL so cookies remain valid through restarts and deployments.

Render prompts for `sync: false` values only when a Blueprint is initially created. For an already-created service, add missing secret variables in the Render dashboard. `Paystack__PlanCodes__Basic`, `__Pro`, and `__Max` must match the plan IDs configured in the Paystack account.

## Connection string handling

Never commit a database password, certificate key, or production connection string. The EF design-time factory reads `ConnectionStrings__Default` from the environment. If it is absent, it uses a non-secret localhost placeholder for migration scaffolding only; database migration commands require a real connection string.

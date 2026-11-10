FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HydroSentinel.csproj ./
RUN dotnet restore HydroSentinel.csproj

COPY . ./
RUN dotnet publish HydroSentinel.csproj --configuration Release --no-restore --output /out/app
RUN dotnet tool restore \
    && dotnet tool run dotnet-ef migrations bundle --context AppDbContext --self-contained --target-runtime linux-x64 --output /out/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=10000
COPY --from=build /out/app ./
COPY --from=build /out/efbundle ./efbundle
ENTRYPOINT ["dotnet", "HydroSentinel.dll"]

/*
 * =====================================================================
 *  SummitPumpCode - FIXED VERSION
 * =====================================================================
 *
 *  WHAT THIS SKETCH DOES
 *    - Fills a tank with an AC pump using two float switches
 *      (start when the LOW float is dry, stop when the HIGH float is wet).
 *    - Detects AC mains with an optocoupler on D3.
 *    - Sends ONE SMS per outage when mains is off AND the tank is low.
 *    - Sends ONE SMS if the pump runs too long without filling the tank.
 *
 *  FIXES COMPARED TO THE ORIGINAL
 *    1. Mains detection no longer depends on loop speed. Every pulse from
 *       the AC detector is captured by an interrupt on D3 (INT1), and the
 *       old delay(50) is gone, so there are no false "no AC" readings.
 *    2. SMS sending is NON-BLOCKING and CONFIRMED. The sketch waits for the
 *       module's ">" prompt and "+CMGS" confirmation, checks SIM and network
 *       registration first, and retries on failure. The "sent" flag is only
 *       set when the module confirms the SMS.
 *    3. The outage must last 30 s (not 1 s) before an SMS is sent, and at
 *       least 30 minutes must pass between outage SMS messages.
 *    4. The "[Device ID]" placeholder is replaced by DEVICE_ID below.
 *    5. The relay is only energised while mains is present and stable
 *       (no battery drain during outages).
 *    6. Restart delay: mains must be stable for 15 s before the pump starts,
 *       and the pump must have been off for 30 s (anti short-cycle).
 *    7. Maximum run time per fill cycle (dry source / stuck float protection).
 *    8. Watchdog timer resets the chip if the sketch ever hangs.
 *    9. checkSIM800L() no longer uses readString() (it blocked for 1 s).
 *
 *  PIN MAP IS UNCHANGED FROM YOUR ORIGINAL SKETCH.
 *  Verify it against your PCB schematic before uploading.
 * =====================================================================
 */

#include <SoftwareSerial.h>
#include <avr/wdt.h>


// =====================================================================
// PIN ASSIGNMENTS (same as the original sketch)
// =====================================================================

const uint8_t MAINS_PIN       = 3;    // D3 (INT1) - AC detector, LOW = AC present
const uint8_t LOW_SWITCH_PIN  = 2;    // D2 - LOW float,  HIGH = float in water
const uint8_t HIGH_SWITCH_PIN = A5;   // A5 - FULL float, HIGH = float in water
const uint8_t RELAY_PIN       = A1;   // A1 - pump relay, HIGH = pump ON
const uint8_t PUMP_LED_PIN    = A0;   // A0 - pump LED
const uint8_t LOW_LED_PIN     = A3;   // A3 - low water LED
const uint8_t FULL_LED_PIN    = A4;   // A4 - full water LED

SoftwareSerial sim800(9, 8);          // RX = D9, TX = D8


// =====================================================================
// USER SETTINGS - EDIT THESE
// =====================================================================

const char DEVICE_ID[]    = "SP-0001";        // unique ID for each unit
const char PHONE_NUMBER[] = "+2348089720011"; // number that receives alerts

// --- Mobile data (SIM800L GPRS) ---
// Replace APN with the APN supplied by the SIM card's mobile carrier.
// Leave username/password empty unless the carrier explicitly requires them.
const char GPRS_APN[]      = "YOUR_CARRIER_APN";
const char GPRS_USERNAME[] = "";
const char GPRS_PASSWORD[] = "";
const unsigned long GPRS_RETRY_MS = 60000UL;
const unsigned long GPRS_CHECK_MS = 300000UL;

// --- Mains detection ---
// The detector gives pulses every 10-20 ms while AC is present. If no pulse
// is seen for this long, AC is considered absent.
const unsigned long MAINS_TIMEOUT_MS = 100UL;

// --- Pump protection ---
const unsigned long MAINS_STABLE_MS = 15000UL;   // mains must be present this long before the pump may start
const unsigned long MIN_OFF_MS      = 30000UL;   // pump must stay off at least this long between runs
const unsigned long MAX_RUN_MS      = 3600000UL; // max pump run time per fill cycle (60 min). TUNE THIS to
                                                 // about 1.5x your normal fill time.

// --- SMS behaviour ---
const unsigned long OUTAGE_CONFIRM_MS = 30000UL;   // no-AC + low-water must last this long before an SMS
const unsigned long SMS_MIN_GAP_MS    = 1800000UL; // minimum 30 min between outage SMS messages
const unsigned long SMS_RETRY_GAP_MS  = 60000UL;   // wait this long before retrying a failed SMS
const uint8_t       SMS_MAX_ATTEMPTS  = 3;         // attempts per event before giving up
const unsigned long GSM_BACKOFF_MS    = 3000UL;    // wait after a GSM step fails, before restarting the init


// =====================================================================
// CONSTANTS USED INTERNALLY
// (plain numbers instead of enums, so the Arduino auto-prototype step
//  can never fail on them)
// =====================================================================

// Alert identifiers
const uint8_t ALERT_NONE            = 0;
const uint8_t ALERT_NO_AC_LOW_WATER = 1;
const uint8_t ALERT_PUMP_FAULT      = 2;

// GSM phases
const uint8_t PH_INIT    = 0;   // bringing the module up (AT, echo off, text mode, SIM, network)
const uint8_t PH_IDLE    = 1;   // module ready, waiting for an SMS request
const uint8_t PH_SMS     = 2;   // sending an SMS
const uint8_t PH_BACKOFF = 3;   // a step failed, waiting before starting over

// How a module response is judged
const uint8_t CHK_OK         = 0;   // response contains "OK"
const uint8_t CHK_SIM_READY  = 1;   // response contains "+CPIN: READY"
const uint8_t CHK_REGISTERED = 2;   // "+CREG: 0,1" (home) or "+CREG: 0,5" (roaming), then "OK"
const uint8_t CHK_PROMPT     = 3;   // module printed the ">" prompt
const uint8_t CHK_SMS_SENT   = 4;   // "+CMGS:" followed by "OK"

// Results of polling a command
const uint8_t CMD_PENDING = 0;
const uint8_t CMD_OK      = 1;
const uint8_t CMD_FAIL    = 2;

// GPRS connection phases
const uint8_t DATA_WAIT          = 0;
const uint8_t DATA_SET_TYPE      = 1;
const uint8_t DATA_SET_APN       = 2;
const uint8_t DATA_SET_USERNAME  = 3;
const uint8_t DATA_SET_PASSWORD  = 4;
const uint8_t DATA_OPEN          = 5;
const uint8_t DATA_VERIFY        = 6;
const uint8_t DATA_ONLINE        = 7;
const uint8_t DATA_BACKOFF       = 8;
const uint8_t DATA_CLOSE         = 9;


// =====================================================================
// MAINS DETECTION
// =====================================================================

// Time of the most recent AC pulse. Written by the interrupt, read by loop().
volatile unsigned long lastMainsPulseMs = 0;

bool mainsPresent = false;            // true while AC pulses keep arriving
unsigned long mainsPresentSince = 0;  // when mains last came back

// Interrupt: runs on every falling edge of the AC detector output.
void mainsPulseISR() {
  lastMainsPulseMs = millis();
}

// Updates mainsPresent / mainsPresentSince.
void updateMains() {

  // Also poll the pin. This covers the case where the detector output stays
  // steadily LOW while AC is present (no falling edges to interrupt on).
  if (digitalRead(MAINS_PIN) == LOW) {
    noInterrupts();
    lastMainsPulseMs = millis();
    interrupts();
  }

  // Read the 4-byte timestamp safely (the interrupt could change it mid-read).
  noInterrupts();
  unsigned long lastPulse = lastMainsPulseMs;
  interrupts();

  // IMPORTANT: millis() is read AFTER the timestamp, so "now" can never be
  // older than lastPulse (which would wrap around and look like a huge gap).
  unsigned long now = millis();

  bool present = (now - lastPulse) <= MAINS_TIMEOUT_MS;

  if (present && !mainsPresent) {
    mainsPresentSince = now;          // mains just came back
    Serial.println(F("Mains: ON"));
  }
  if (!present && mainsPresent) {
    Serial.println(F("Mains: OFF"));
  }

  mainsPresent = present;
}


// =====================================================================
// GSM (SIM800L) - NON-BLOCKING STATE MACHINE
// =====================================================================

const uint8_t RX_BUF_SIZE = 96;
char rxBuf[RX_BUF_SIZE];              // collects the module's reply to the current command
uint8_t rxLen = 0;

unsigned long cmdStartedAt = 0;       // when the current command was sent
unsigned long cmdTimeoutMs = 0;       // how long we wait for its reply
bool cmdInFlight = false;             // true while waiting for a reply

uint8_t dataPhase = DATA_WAIT;
bool dataCommandBusy = false;         // shares the SIM800 serial command channel with SMS
unsigned long dataPhaseAt = 0;
unsigned long dataLastCheckAt = 0;

uint8_t gsmPhase = PH_INIT;           // current phase (PH_*)
uint8_t gsmStep  = 0;                 // step within the phase
unsigned long backoffStartedAt = 0;

char smsText[161];                    // text of the SMS being sent
uint8_t smsAlertId = ALERT_NONE;      // which alert that SMS belongs to

bool smsResultReady = false;          // set when an SMS attempt finishes
uint8_t smsResultId = ALERT_NONE;
bool smsResultOk = false;


// Starts waiting for a reply: clears old data and arms the timeout.
void cmdBegin(unsigned long timeoutMs) {
  while (sim800.available()) {
    sim800.read();                    // discard stale bytes
  }
  rxLen = 0;
  rxBuf[0] = '\0';
  cmdStartedAt = millis();
  cmdTimeoutMs = timeoutMs;
  cmdInFlight = true;
}

// Sends an AT command line and starts waiting for the reply.
void cmdSend(const char* cmd, unsigned long timeoutMs) {
  cmdBegin(timeoutMs);
  sim800.print(cmd);
  sim800.print("\r\n");
}

// Reads whatever has arrived and decides if the reply is complete.
// Returns CMD_PENDING (keep waiting), CMD_OK or CMD_FAIL (including timeout).
uint8_t cmdPoll(uint8_t check) {

  while (sim800.available()) {
    char c = (char)sim800.read();
    if (rxLen < RX_BUF_SIZE - 1) {
      rxBuf[rxLen++] = c;
      rxBuf[rxLen] = '\0';
    }
  }

  bool hasOk    = (strstr(rxBuf, "OK") != NULL);
  bool hasError = (strstr(rxBuf, "ERROR") != NULL);   // also matches "+CME ERROR" / "+CMS ERROR"

  if (hasError) {
    return CMD_FAIL;
  }

  switch (check) {

    case CHK_OK:
      if (hasOk) return CMD_OK;
      break;

    case CHK_SIM_READY:
      if (strstr(rxBuf, "+CPIN: READY") != NULL) return CMD_OK;
      if (hasOk) return CMD_FAIL;                      // finished but SIM is not ready
      break;

    case CHK_REGISTERED:
      if (hasOk) {
        if (strstr(rxBuf, ",1") != NULL || strstr(rxBuf, ",5") != NULL) {
          return CMD_OK;
        }
        return CMD_FAIL;                               // finished but not registered
      }
      break;

    case CHK_PROMPT:
      if (strchr(rxBuf, '>') != NULL) return CMD_OK;
      break;

    case CHK_SMS_SENT:
      if (strstr(rxBuf, "+CMGS:") != NULL && hasOk) return CMD_OK;
      break;
  }

  if (millis() - cmdStartedAt >= cmdTimeoutMs) {
    return CMD_FAIL;                                   // timed out
  }

  return CMD_PENDING;
}

// Called when any GSM step fails.
void gsmFail() {

  if (gsmPhase == PH_SMS) {
    Serial.println(F("SMS attempt FAILED"));
    smsResultReady = true;
    smsResultId    = smsAlertId;
    smsResultOk    = false;
    sim800.write(27);                 // ESC: leave the ">" prompt if the module is stuck in it
  } else {
    Serial.println(F("GSM init step failed, will retry"));
  }

  cmdInFlight = false;
  gsmPhase = PH_BACKOFF;
  backoffStartedAt = millis();
}

// INIT phase: AT -> echo off -> text mode -> SIM ready -> network registered.
void gsmServiceInit() {

  if (!cmdInFlight) {
    switch (gsmStep) {
      case 0: cmdSend("AT",        1000); break;   // is the module alive?
      case 1: cmdSend("ATE0",      1000); break;   // echo off, so our own text is never echoed back
      case 2: cmdSend("AT+CMGF=1", 2000); break;   // SMS text mode
      case 3: cmdSend("AT+CPIN?",  2000); break;   // SIM inserted and unlocked?
      case 4: cmdSend("AT+CREG?",  2000); break;   // registered on the network?
    }
    return;
  }

  uint8_t check = CHK_OK;
  if (gsmStep == 3) check = CHK_SIM_READY;
  if (gsmStep == 4) check = CHK_REGISTERED;

  uint8_t status = cmdPoll(check);
  if (status == CMD_PENDING) return;

  cmdInFlight = false;

  if (status == CMD_FAIL) {
    gsmFail();
    return;
  }

  gsmStep++;
  if (gsmStep > 4) {
    gsmPhase = PH_IDLE;
    Serial.println(F("GSM ready"));
  }
}

// SMS phase: re-check network -> AT+CMGS -> wait for ">" -> send text + Ctrl-Z -> wait for confirmation.
void gsmServiceSms() {

  if (!cmdInFlight) {
    switch (gsmStep) {

      case 0:
        cmdSend("AT+CREG?", 2000);                   // still registered?
        break;

      case 1:
        cmdBegin(5000);
        sim800.print(F("AT+CMGS=\""));
        sim800.print(PHONE_NUMBER);
        sim800.print(F("\"\r\n"));
        break;

      case 2:
        cmdBegin(40000UL);                           // networks can be slow, allow up to 40 s
        sim800.print(smsText);
        sim800.write(26);                            // Ctrl-Z ends the message
        break;
    }
    return;
  }

  uint8_t check = CHK_REGISTERED;
  if (gsmStep == 1) check = CHK_PROMPT;
  if (gsmStep == 2) check = CHK_SMS_SENT;

  uint8_t status = cmdPoll(check);
  if (status == CMD_PENDING) return;

  cmdInFlight = false;

  if (status == CMD_FAIL) {
    gsmFail();
    return;
  }

  gsmStep++;
  if (gsmStep > 2) {
    Serial.println(F("SMS sent (confirmed by module)"));
    smsResultReady = true;
    smsResultId    = smsAlertId;
    smsResultOk    = true;
    gsmPhase = PH_IDLE;
  }
}

// Must be called on every pass of loop().
void gsmService() {
  switch (gsmPhase) {

    case PH_INIT:
      gsmServiceInit();
      break;

    case PH_IDLE:
      break;

    case PH_SMS:
      gsmServiceSms();
      break;

    case PH_BACKOFF:
      if (millis() - backoffStartedAt >= GSM_BACKOFF_MS) {
        gsmPhase = PH_INIT;                          // start over from "AT"
        gsmStep = 0;
        cmdInFlight = false;
      }
      break;
  }
}

// True when the module is ready to accept a new SMS.
bool gsmIsReady() {
  return gsmPhase == PH_IDLE && !dataCommandBusy;
}

// Start the SIM800 packet-data bearer without blocking the pump-control loop.
void dataFail(const __FlashStringHelper* reason) {
  Serial.print(F("GPRS setup failed: "));
  Serial.println(reason);
  cmdInFlight = false;
  dataCommandBusy = false;
  dataPhase = DATA_BACKOFF;
  dataPhaseAt = millis();
}

bool modemValueIsSafe(const char* value) {
  if (value == NULL || value[0] == '\0') return false;
  for (const char* p = value; *p; ++p) {
    if (*p == '"' || *p == '\r' || *p == '\n') return false;
  }
  return true;
}

void dataSend(const char* command, unsigned long timeoutMs) {
  cmdSend(command, timeoutMs);
  dataCommandBusy = true;
}

void dataService() {
  if (gsmPhase != PH_IDLE) return; // never overlap modem commands with SMS setup/sending

  unsigned long now = millis();
  if (!modemValueIsSafe(GPRS_APN) || strcmp(GPRS_APN, "YOUR_CARRIER_APN") == 0) {
    if (dataPhase != DATA_BACKOFF) {
      Serial.println(F("Set GPRS_APN to your SIM carrier's APN to enable mobile data."));
      dataPhase = DATA_BACKOFF;
      dataPhaseAt = now;
    }
    return;
  }

  char command[128];
  uint8_t status;

  switch (dataPhase) {
    case DATA_WAIT:
      dataPhase = DATA_SET_TYPE;
      break;

    case DATA_SET_TYPE:
      if (!cmdInFlight) {
        dataSend("AT+SAPBR=3,1,\"Contype\",\"GPRS\"", 5000);
        return;
      }
      status = cmdPoll(CHK_OK);
      if (status == CMD_PENDING) return;
      if (status == CMD_FAIL) { dataFail(F("bearer type")); return; }
      dataCommandBusy = false;
      cmdInFlight = false;
      dataPhase = DATA_SET_APN;
      break;

    case DATA_SET_APN:
      if (!cmdInFlight) {
        snprintf(command, sizeof(command), "AT+SAPBR=3,1,\"APN\",\"%s\"", GPRS_APN);
        dataSend(command, 5000);
        return;
      }
      status = cmdPoll(CHK_OK);
      if (status == CMD_PENDING) return;
      if (status == CMD_FAIL) { dataFail(F("APN")); return; }
      dataCommandBusy = false;
      cmdInFlight = false;
      dataPhase = (GPRS_USERNAME[0] != '\0') ? DATA_SET_USERNAME : DATA_OPEN;
      break;

    case DATA_SET_USERNAME:
      if (!cmdInFlight) {
        if (!modemValueIsSafe(GPRS_USERNAME)) { dataFail(F("username contains unsupported characters")); return; }
        snprintf(command, sizeof(command), "AT+SAPBR=3,1,\"USER\",\"%s\"", GPRS_USERNAME);
        dataSend(command, 5000);
        return;
      }
      status = cmdPoll(CHK_OK);
      if (status == CMD_PENDING) return;
      if (status == CMD_FAIL) { dataFail(F("APN username")); return; }
      dataCommandBusy = false;
      cmdInFlight = false;
      dataPhase = (GPRS_PASSWORD[0] != '\0') ? DATA_SET_PASSWORD : DATA_OPEN;
      break;

    case DATA_SET_PASSWORD:
      if (!cmdInFlight) {
        if (!modemValueIsSafe(GPRS_PASSWORD)) { dataFail(F("password contains unsupported characters")); return; }
        snprintf(command, sizeof(command), "AT+SAPBR=3,1,\"PWD\",\"%s\"", GPRS_PASSWORD);
        dataSend(command, 5000);
        return;
      }
      status = cmdPoll(CHK_OK);
      if (status == CMD_PENDING) return;
      if (status == CMD_FAIL) { dataFail(F("APN password")); return; }
      dataCommandBusy = false;
      cmdInFlight = false;
      dataPhase = DATA_OPEN;
      break;

    case DATA_OPEN:
      if (!cmdInFlight) {
        Serial.println(F("Opening mobile-data connection..."));
        dataSend("AT+SAPBR=1,1", 90000UL);
        return;
      }
      status = cmdPoll(CHK_OK);
      if (status == CMD_PENDING) return;
      if (status == CMD_FAIL) { dataFail(F("network attach; check SIM data plan, signal, and APN")); return; }
      dataCommandBusy = false;
      cmdInFlight = false;
      dataPhase = DATA_VERIFY;
      break;

    case DATA_VERIFY:
      if (!cmdInFlight) {
        dataSend("AT+SAPBR=2,1", 5000);
        return;
      }
      status = cmdPoll(CHK_OK);
      if (status == CMD_PENDING) return;
      if (status == CMD_FAIL || strstr(rxBuf, "+SAPBR: 1,1,\"") == NULL || strstr(rxBuf, "0.0.0.0") != NULL) {
        dataFail(F("no mobile-data IP address"));
        return;
      }
      dataCommandBusy = false;
      cmdInFlight = false;
      dataPhase = DATA_ONLINE;
      dataLastCheckAt = now;
      Serial.print(F("Mobile data connected; modem response: "));
      Serial.println(rxBuf);
      break;

    case DATA_ONLINE:
      if (now - dataLastCheckAt >= GPRS_CHECK_MS) dataPhase = DATA_VERIFY;
      break;

    case DATA_BACKOFF:
      if (now - dataPhaseAt >= GPRS_RETRY_MS) dataPhase = DATA_CLOSE;
      break;

    case DATA_CLOSE:
      if (!cmdInFlight) {
        dataSend("AT+SAPBR=0,1", 10000);
        return;
      }
      // Closing may report ERROR when the bearer is already down; either way,
      // clear the command and start a fresh setup attempt.
      status = cmdPoll(CHK_OK);
      if (status == CMD_PENDING) return;
      dataCommandBusy = false;
      cmdInFlight = false;
      dataPhase = DATA_WAIT;
      break;
  }
}

// Asks the GSM state machine to send an SMS. Returns false if it is busy
// or not ready (nothing is queued in that case).
bool gsmSendSms(uint8_t alertId, const char* text) {
  if (gsmPhase != PH_IDLE || dataCommandBusy) return false;

  strncpy(smsText, text, sizeof(smsText) - 1);
  smsText[sizeof(smsText) - 1] = '\0';

  smsAlertId = alertId;
  gsmPhase = PH_SMS;
  gsmStep = 0;
  cmdInFlight = false;
  return true;
}

// Collects the result of the last SMS attempt (once). Returns false if none is waiting.
bool gsmTakeResult(uint8_t &alertId, bool &ok) {
  if (!smsResultReady) return false;
  alertId = smsResultId;
  ok = smsResultOk;
  smsResultReady = false;
  return true;
}


// =====================================================================
// PUMP STATE
// =====================================================================

bool pumpWanted  = false;           // latching decision from the float switches
bool pumpRunning = false;           // relay is actually energised
bool pumpFault   = false;           // max-run-time exceeded; cleared only by RESET / power cycle

bool hasStopped = false;            // has the pump stopped at least once since boot?
unsigned long lastStopAt = 0;       // when it last stopped

unsigned long cycleRunMs = 0;       // pump run time accumulated in the current fill cycle
unsigned long lastLoopMs = 0;       // used to measure the time between loop passes


// =====================================================================
// ALERT STATE
// =====================================================================

// --- No AC + low water ---
bool outageActive = false;                // condition currently true
unsigned long outageSince = 0;            // when the condition started
bool outageAlertSent = false;             // SMS confirmed for this outage
uint8_t outageAttempts = 0;
unsigned long lastOutageAttemptAt = 0;
bool hasSentOutageSms = false;
unsigned long lastOutageSmsAt = 0;

// --- Pump fault ---
bool faultAlertSent = false;
uint8_t faultAttempts = 0;
unsigned long lastFaultAttemptAt = 0;


// Returns true during the first half of every periodMs (used to blink LEDs).
bool blinkPhase(unsigned long periodMs) {
  return ((millis() / periodMs) & 1UL) == 0;
}


// =====================================================================
// SETUP
// =====================================================================

void setup() {

  // AC detector: pulled up, pulled LOW by the optocoupler while AC is present.
  pinMode(MAINS_PIN, INPUT_PULLUP);

  // Float switches: HIGH = float in water, LOW = dry.
  pinMode(LOW_SWITCH_PIN, INPUT);
  pinMode(HIGH_SWITCH_PIN, INPUT);

  // Outputs
  pinMode(RELAY_PIN, OUTPUT);
  pinMode(PUMP_LED_PIN, OUTPUT);
  pinMode(LOW_LED_PIN, OUTPUT);
  pinMode(FULL_LED_PIN, OUTPUT);

  // Safe initial state: pump OFF, LEDs OFF.
  digitalWrite(RELAY_PIN, LOW);
  digitalWrite(PUMP_LED_PIN, LOW);
  digitalWrite(LOW_LED_PIN, LOW);
  digitalWrite(FULL_LED_PIN, LOW);

  Serial.begin(9600);
  sim800.begin(9600);

  // Start with "no AC seen yet": make the last pulse look older than the timeout.
  // (Unsigned arithmetic wraps correctly.)
  lastMainsPulseMs = millis() - (MAINS_TIMEOUT_MS + 1UL);

  // Capture every pulse from the AC detector, no matter what loop() is doing.
  attachInterrupt(digitalPinToInterrupt(MAINS_PIN), mainsPulseISR, FALLING);

  lastLoopMs = millis();

  // Watchdog: if loop() ever stops running for 4 s, the chip resets and the relay drops out.
  wdt_enable(WDTO_4S);

  Serial.println(F("Pump controller started"));
}


// =====================================================================
// MAIN LOOP
// =====================================================================

void loop() {

  wdt_reset();                                   // tell the watchdog we are alive

  // ---------------------------------------------------------------
  // 1. MAINS + TIMING
  // ---------------------------------------------------------------
  updateMains();

  unsigned long now = millis();
  unsigned long dt = now - lastLoopMs;           // time since the previous pass
  lastLoopMs = now;

  // Mains counts as "stable" only after it has been present for MAINS_STABLE_MS.
  bool mainsStable = mainsPresent && (now - mainsPresentSince >= MAINS_STABLE_MS);

  // ---------------------------------------------------------------
  // 2. FLOAT SWITCHES + LEVEL LEDS
  // ---------------------------------------------------------------
  bool lowFloatInWater  = (digitalRead(LOW_SWITCH_PIN)  == HIGH);
  bool highFloatInWater = (digitalRead(HIGH_SWITCH_PIN) == HIGH);

  digitalWrite(LOW_LED_PIN,  lowFloatInWater  ? LOW  : HIGH);   // low-water LED on when LOW float is dry
  digitalWrite(FULL_LED_PIN, highFloatInWater ? HIGH : LOW);    // full LED on when HIGH float is wet

  // ---------------------------------------------------------------
  // 3. LATCHING PUMP DECISION (unchanged logic)
  //      HIGH float wet : stop (and the fill cycle is complete)
  //      LOW float dry  : start
  //      otherwise      : keep the current decision
  // ---------------------------------------------------------------
  if (highFloatInWater) {
    pumpWanted = false;
    cycleRunMs = 0;                              // tank is full, so the next fill cycle starts from zero
  } else if (!lowFloatInWater) {
    pumpWanted = true;
  }

  // ---------------------------------------------------------------
  // 4. MAXIMUM RUN TIME (dry source / stuck or broken float)
  // ---------------------------------------------------------------
  if (pumpRunning) {
    cycleRunMs += dt;
  }

  if (!pumpFault && cycleRunMs >= MAX_RUN_MS) {
    pumpFault = true;                            // latched until RESET / power cycle
    Serial.println(F("FAULT: max pump run time exceeded"));
  }

  // ---------------------------------------------------------------
  // 5. RELAY CONTROL
  //    The pump may only run when: it is wanted, mains has been stable,
  //    and there is no fault. This also means the relay coil is NOT held
  //    on from the backup battery during an outage.
  // ---------------------------------------------------------------
  bool canRun = pumpWanted && mainsStable && !pumpFault;

  if (pumpRunning) {

    if (!canRun) {
      digitalWrite(RELAY_PIN, LOW);
      pumpRunning = false;
      hasStopped = true;
      lastStopAt = now;
      Serial.println(F("Pump: STOP"));
    }

  } else {

    // Anti short-cycle: after a stop, wait MIN_OFF_MS before starting again.
    bool offLongEnough = !hasStopped || (now - lastStopAt >= MIN_OFF_MS);

    if (canRun && offLongEnough) {
      digitalWrite(RELAY_PIN, HIGH);
      pumpRunning = true;
      Serial.println(F("Pump: START"));
    }
  }

  // ---------------------------------------------------------------
  // 6. PUMP LED
  //      solid       = pump running
  //      fast blink  = fault
  //      slow blink  = pump wanted but waiting (no mains / stabilising / min-off time)
  //      off         = idle
  // ---------------------------------------------------------------
  if (pumpFault) {
    digitalWrite(PUMP_LED_PIN, blinkPhase(150) ? HIGH : LOW);
  } else if (pumpRunning) {
    digitalWrite(PUMP_LED_PIN, HIGH);
  } else if (pumpWanted) {
    digitalWrite(PUMP_LED_PIN, blinkPhase(500) ? HIGH : LOW);
  } else {
    digitalWrite(PUMP_LED_PIN, LOW);
  }

  // ---------------------------------------------------------------
  // 7. GSM STATE MACHINE (never blocks)
  // ---------------------------------------------------------------
  gsmService();
  dataService();

  // ---------------------------------------------------------------
  // 8. COLLECT THE RESULT OF A FINISHED SMS ATTEMPT
  //    A flag is only set when the module CONFIRMED the SMS.
  // ---------------------------------------------------------------
  uint8_t doneId;
  bool doneOk;

  if (gsmTakeResult(doneId, doneOk)) {

    if (doneId == ALERT_NO_AC_LOW_WATER && doneOk) {
      outageAlertSent = true;
      hasSentOutageSms = true;
      lastOutageSmsAt = now;
    }

    if (doneId == ALERT_PUMP_FAULT && doneOk) {
      faultAlertSent = true;
    }
  }

  // ---------------------------------------------------------------
  // 9. ALERT: NO AC + LOW WATER
  // ---------------------------------------------------------------
  bool waterIsLow = !lowFloatInWater;

  if (!mainsPresent && waterIsLow) {

    if (!outageActive) {
      outageActive = true;
      outageSince = now;
    }

  } else {
    // AC is back, or the water is no longer low: reset for the next outage.
    outageActive = false;
    outageAlertSent = false;
    outageAttempts = 0;
  }

  if (outageActive &&
      (now - outageSince >= OUTAGE_CONFIRM_MS) &&                                 // not a short flicker
      !outageAlertSent &&
      outageAttempts < SMS_MAX_ATTEMPTS &&
      (outageAttempts == 0 || now - lastOutageAttemptAt >= SMS_RETRY_GAP_MS) &&   // spacing between retries
      (!hasSentOutageSms || now - lastOutageSmsAt >= SMS_MIN_GAP_MS) &&           // spacing between outage SMS messages
      gsmIsReady()) {

    char msg[161];
    snprintf(msg, sizeof(msg),
             "Dear Customer, your HydroSentinel order %s has reported low water level and no AC input discovered. Please provide an alternative power supply.",
             DEVICE_ID);

    if (gsmSendSms(ALERT_NO_AC_LOW_WATER, msg)) {
      outageAttempts++;
      lastOutageAttemptAt = now;
      Serial.println(F("Sending outage SMS..."));
    }
  }

  // ---------------------------------------------------------------
  // 10. ALERT: PUMP FAULT (max run time exceeded)
  // ---------------------------------------------------------------
  if (pumpFault &&
      !faultAlertSent &&
      faultAttempts < SMS_MAX_ATTEMPTS &&
      (faultAttempts == 0 || now - lastFaultAttemptAt >= SMS_RETRY_GAP_MS) &&
      gsmIsReady()) {

    char msg[161];
    snprintf(msg, sizeof(msg),
             "Dear Customer, your HydroSentinel order %s: pump stopped, tank not filling in time. Check water source and floats, then press RESET.",
             DEVICE_ID);

    if (gsmSendSms(ALERT_PUMP_FAULT, msg)) {
      faultAttempts++;
      lastFaultAttemptAt = now;
      Serial.println(F("Sending fault SMS..."));
    }
  }

  // No delay() here on purpose: loop() must run fast.
}

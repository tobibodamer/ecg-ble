//#include <phyphoxBle.h> 
#include <ArduinoBLE.h>

unsigned short POLLING_RATE = 1000; // In Hz
unsigned long POLLING_DELAY = 1000000 / POLLING_RATE;

unsigned const long SEND_BUFFER_SIZE = 20;

BLEService ecgService("2a264ccf-a9ca-4097-8efd-c5b6fba390a6");
BLECharacteristic ecgSignalChar("2a264cdf-a9ca-4097-8efd-c5b6fba390a6", BLERead | BLENotify, SEND_BUFFER_SIZE, false);
BLEUnsignedShortCharacteristic pollingRateChar("2a264cef-a9ca-4097-8efd-c5b6fba390a6", BLERead | BLEWrite);

BLEDescriptor pollingRateDescriptor("2901", "Polling Rate");
BLEDescriptor ecgSignalDescriptor("2901", "ECG Raw Data");

void setup() {
  Serial.begin(9600);
  //while (!Serial);

  pinMode(LED_BUILTIN, OUTPUT);
  if (!BLE.begin()) 
  {
    Serial.println("starting BLE failed!");
    while (1);
  }
  
  BLE.setLocalName("ECG Monitor");
  BLE.setAdvertisedService(ecgService);
  
  ecgService.addCharacteristic(ecgSignalChar);
  ecgService.addCharacteristic(pollingRateChar);

  pollingRateChar.writeValue(POLLING_RATE);
  pollingRateChar.setEventHandler(BLEWritten, switchPollingRate);
  pollingRateChar.addDescriptor(pollingRateDescriptor);

  ecgSignalChar.addDescriptor(ecgSignalDescriptor);
  
  BLE.addService(ecgService);
  
  BLE.advertise();
  Serial.println("Bluetooth device active, waiting for connections...");
}

void switchPollingRate(BLEDevice central, BLECharacteristic characteristic) {
  unsigned short newPollingRate = pollingRateChar.value();
  Serial.print("Switching polling rate to ");
  Serial.println(newPollingRate);
  POLLING_RATE = newPollingRate;
  POLLING_DELAY = 1000000 / POLLING_RATE;
}


unsigned int sendBufferIndex = 0;
unsigned int payloadIndex = 0;

byte sendBuffer[SEND_BUFFER_SIZE];
uint16_t fourValues[4];

unsigned long lastPoll;
int input;
void loop() 
{
  BLEDevice central = BLE.central();
  
  if (central) 
  {
    Serial.print("Connected to central: ");
    Serial.println(central.address());
    digitalWrite(LED_BUILTIN, HIGH);
    
    while (central.connected()) {
      unsigned long currMicros = micros();
      unsigned long diff = currMicros - lastPoll;
      if (diff < POLLING_DELAY)
      {
        continue;
      }

      input = analogRead(A0);
      
      fourValues[payloadIndex++] = input;

      lastPoll = currMicros;
      
      if (payloadIndex >= 4) {
        sendBuffer[sendBufferIndex++] = (byte)(fourValues[0] >> 2);
        sendBuffer[sendBufferIndex++] = (byte)((fourValues[0] & 0b0000000011) << 6 | (fourValues[1] >> 4));
        sendBuffer[sendBufferIndex++] = (byte)((fourValues[1] & 0b0000001111) << 4 | (fourValues[2] >> 6));
        sendBuffer[sendBufferIndex++] = (byte)((fourValues[2] & 0b0000111111) << 2 | (fourValues[3] >> 8));
        sendBuffer[sendBufferIndex++] = (byte)((fourValues[3] & 0b0011111111) << 0);

        payloadIndex = 0;
      }

      if (sendBufferIndex >= SEND_BUFFER_SIZE) {
        ecgSignalChar.writeValue(sendBuffer, SEND_BUFFER_SIZE, false);
        sendBufferIndex = 0;
      }

      //Serial.println(input);
      
      //Serial.print("Polling time error: ");
      Serial.println(diff - POLLING_DELAY);
      //Serial.println(" ms");
      
    }
  }
  digitalWrite(LED_BUILTIN, LOW);
  Serial.print("Disconnected from central: ");
  Serial.println(central.address());
}

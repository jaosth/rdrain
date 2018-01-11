#include <DallasTemperature.h>
#include <OneWire.h>

// Pin definitions
const uint8_t Pin_OneWireBus = 2;
const uint8_t Pin_InputDrain = 3;
const uint8_t Pin_InputPrime = 4;
const uint8_t Pin_OutputPumpX = 5;
const uint8_t Pin_OutputPumpY = 6;
const uint8_t Pin_InputButton = 7;
const uint8_t Pin_OutputLedBuiltIn = 13;

// Algorithm parameters
const float LowerFreezingCutoffCelsius = 1.0f;
const float UpperFreezingCutoffCelsius = 1.5f;
const uint32_t RetryDrainInitialDelay = 3600000; // 60 * 60 * 1000; 30 minutes
const uint32_t RetryDrainMaxDelay = 604800000; // 7 * 24 * 60 * 60 * 1000; 1 week
const uint32_t RetryPrimeWhileDrainingDelay = 3600000; // 60 * 60 * 1000; 1 hour
const uint32_t ReadTemperatureDelay = 30000; // 30 * 1000; 30 seconds

// Strongly-typed binary pin state
enum class PinState : uint8_t
{
	Low = 0,
	High = 1
};

// Input pins
enum class InputPin : uint8_t
{
	Drain = Pin_InputDrain,
	Prime = Pin_InputPrime,
	Button = Pin_InputButton
};

// Output pins
enum class OutputPin : uint8_t
{
	PumpX = Pin_OutputPumpX,
	PumpY = Pin_OutputPumpY,
	LedBuiltIn = Pin_OutputLedBuiltIn
};

// Pump names to flags
enum class Pump : uint8_t
{
	None = 0b00,
	A = 0b01,
	B = 0b10,
	C = 0b11
};

// Global state
OneWire oneWire(Pin_OneWireBus);
DallasTemperature sensors(&oneWire);
float currentTemperature;
bool isFrozen;
uint32_t timeOfLastPrime;
uint32_t timeOfLastTempCheck;
uint32_t timeOfLastDrain;
uint32_t retryDrainCurrentDelay;
PinState userLedState;

// One-time initialization
void setup() {
	uint32_t now = millis();

	pinMode(Pin_InputPrime, INPUT);
	pinMode(Pin_InputDrain, INPUT);
	pinMode(Pin_OutputPumpX, OUTPUT);
	pinMode(Pin_OutputPumpY, OUTPUT);
	pinMode(Pin_InputButton, INPUT);
	pinMode(Pin_OutputLedBuiltIn, OUTPUT);

	Serial.begin(9600);

	sensors.begin();
	isFrozen = safeReadTemperature() < LowerFreezingCutoffCelsius;

	timeOfLastDrain = now;
	timeOfLastTempCheck = now - ReadTemperatureDelay;
	timeOfLastPrime = now;
	retryDrainCurrentDelay = RetryDrainMaxDelay;
	userLedState = PinState::Low;
	enablePump(Pump::None);
}

// Read from an input pin
PinState safeDigitalRead(InputPin inputPin)
{
	return digitalRead(static_cast<uint8_t>(inputPin)) == HIGH ? PinState::High : PinState::Low;
}

// Write to an output pin
void safeDigitalWrite(OutputPin outputPin, PinState pinState)
{
	digitalWrite(static_cast<uint8_t>(outputPin), pinState == PinState::High ? HIGH : LOW);
}

// Enable a pump
void enablePump(Pump pump)
{
	digitalWrite(Pin_OutputPumpX, (static_cast<uint8_t>(pump) & 1) ? HIGH : LOW);
	digitalWrite(Pin_OutputPumpY, ((static_cast<uint8_t>(pump) >> 1) & 1) ? HIGH : LOW);
}

// Wait for a given expected state on a pin to hold
PinState tryWaitForState(InputPin inputPin, PinState test, uint32_t holdForMilliseconds, uint32_t maxWaitMilliseconds)
{
	uint32_t now = millis();
	uint32_t start = now;
	uint32_t holdFrom = now;
	PinState inputState;

	do
	{
		now = millis();
		inputState = safeDigitalRead(inputPin);
		holdFrom = inputState == test ? holdFrom : now;

		// Serial.print(static_cast<uint8_t>(test));
		// Serial.print(" ");
		// Serial.print(static_cast<uint8_t>(inputState));
		// Serial.print(" ");
		// Serial.print(now - holdFrom);
		// Serial.print(" ");    
		// Serial.print(holdForMilliseconds);
		// Serial.print(" ");    
		// Serial.print(now - start);
		// Serial.print(" ");   
		// Serial.print(maxWaitMilliseconds);
		// Serial.println();       
	} while ((now - holdFrom < holdForMilliseconds) && (now - start < maxWaitMilliseconds));

	return inputState;
}

// Prime a siphon pump
bool prime(Pump pump)
{
	printStatusLine("Priming");

	if (tryWaitForState(InputPin::Prime, PinState::High, 5 * 1000, 30000) == PinState::Low)
	{
		// Serial.println(" ready failed!");
		return false;
	}

	enablePump(pump);

	if (tryWaitForState(InputPin::Prime, PinState::Low, 10 * 1000, 60000) == PinState::High)
	{
		// Serial.println(" prime failed!");
		enablePump(Pump::None);
		return false;
	}

	enablePump(Pump::None);

	// Serial.println(" success!");
	return true;
}

// Reads the temperature while ignoring incorrect temperature values due to power brownouts
float safeReadTemperature()
{
	float result;

	do
	{
		delay(1000);
		sensors.requestTemperatures();
		delay(1000);
		result = sensors.getTempCByIndex(0);
		// Serial.println(result);
	} while (result < -50.0 || result > 50); // Impossible readings for outdoors in Seattle area

	return result;
}

// Used to flush the pumps
void runFlushSequence()
{
	safeDigitalWrite(OutputPin::LedBuiltIn, PinState::Low);
	enablePump(Pump::A);
	delay(1000);
	enablePump(Pump::B);
	delay(1000);
	enablePump(Pump::C);
	delay(1000);
	enablePump(Pump::None);
	delay(1000);
}

// Used to prime the pumps
void runPrimeSequence()
{
	safeDigitalWrite(OutputPin::LedBuiltIn, PinState::Low);
	prime(Pump::A);
	prime(Pump::B);
	prime(Pump::C);
	delay(1000);
}

// Print current status
void printStatusLine(char* message)
{
	uint32_t now = millis();
	bool draining = safeDigitalRead(InputPin::Drain) == PinState::Low;
	uint32_t timeOfNextPrime = draining ? timeOfLastPrime + RetryPrimeWhileDrainingDelay : timeOfLastPrime + retryDrainCurrentDelay;

	Serial.print("{ ");
	Serial.print("\"currentTemperature\": "); Serial.print(currentTemperature); Serial.print(", ");
	Serial.print("\"isFrozen\": "); Serial.print(isFrozen); Serial.print(", ");
	Serial.print("\"currentTime\": "); Serial.print(now); Serial.print(", ");
	Serial.print("\"timeOfLastPrime\": "); Serial.print(timeOfLastPrime); Serial.print(", ");
	Serial.print("\"timeOfLastDrain\": "); Serial.print(timeOfLastDrain); Serial.print(", ");
	Serial.print("\"timeOfNextPrime\": "); Serial.print(timeOfNextPrime); Serial.print(", ");
	Serial.print("\"isDraining\": "); Serial.print(draining ? "true" : "false"); Serial.print(", ");
	Serial.print("\"message\": \""); Serial.print(message); Serial.print("\" ");
	Serial.println(" }");
}

// Repeat indefinitely
void loop()
{
	uint32_t now = millis();

	// In case there was an issue, disable the pump
	enablePump(Pump::None);

	// Checking too rapidly causes bad readings so check with a delay
	if (now - timeOfLastTempCheck > ReadTemperatureDelay)
	{
		timeOfLastTempCheck = now;
		currentTemperature = safeReadTemperature();
	}

	PinState inputDrainState = safeDigitalRead(InputPin::Drain);
	PinState inputButtonState = safeDigitalRead(InputPin::Button);

	if (currentTemperature < LowerFreezingCutoffCelsius && !isFrozen)
	{
		isFrozen = true;
		runFlushSequence();
	}
	else if (currentTemperature > UpperFreezingCutoffCelsius && isFrozen)
	{
		isFrozen = false;
	}

	printStatusLine("Online");

	// If button is pushed or drain message is sent (single 'd'), prime the pumps regardless of whether or not we are frozen
	// Also reset the drain delay in case button is pushed too early in rainstorm

	if (inputButtonState == PinState::Low ||
		(Serial.available() && Serial.read() == 'd' && Serial.read() == -1))
	{
		timeOfLastPrime = now;
		runPrimeSequence();

		timeOfLastDrain = now;
		retryDrainCurrentDelay = RetryDrainInitialDelay;
	}

	// Flush serial input buffer
	while (Serial.available() && Serial.read() != -1);

	// If actively draining
	if (inputDrainState == PinState::Low)
	{
		timeOfLastDrain = now;
		retryDrainCurrentDelay = RetryDrainInitialDelay;

		// Re-prime after a delay
		if (now - timeOfLastPrime > RetryPrimeWhileDrainingDelay)
		{
			timeOfLastPrime = now;
			runPrimeSequence();
		}
	}
	// If not actively draining
	else
	{
		// Use exponential backoff reprime when not frozen
		if (!isFrozen && now - timeOfLastDrain > retryDrainCurrentDelay)
		{
			timeOfLastPrime = now;
			runPrimeSequence();

			retryDrainCurrentDelay = retryDrainCurrentDelay << 1;

			if (retryDrainCurrentDelay > RetryDrainMaxDelay)
			{
				retryDrainCurrentDelay = RetryDrainMaxDelay;
			}
			else if (retryDrainCurrentDelay < RetryDrainInitialDelay)
			{
				// Sanity bounds in case shifting logic causes an issue that takes the delay too low
				retryDrainCurrentDelay = RetryDrainInitialDelay;
			}
		}
	}

	// Draining indicator
	safeDigitalWrite(OutputPin::LedBuiltIn, userLedState);
	userLedState = (userLedState == PinState::High ? PinState::Low : inputDrainState == PinState::Low ? PinState::High : PinState::Low);
}

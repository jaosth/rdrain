#!/usr/bin/python
import serial,time,requests,json

def getSerial():
    ser = serial.Serial("/dev/ttyACM0",9600)
    ser.bytesize = serial.EIGHTBITS
    ser.parity = serial.PARITY_NONE
    ser.stopbits = serial.STOPBITS_ONE
    ser.timeout = None
    ser.xonxoff = False
    ser.rtscts = False
    ser.dsrdtr = False
    ser.writeTimeout = 0

    if not ser.isOpen():
        try:
            ser.open()
        except Exception, e:
            print "error open serial port: " + str(e)
            exit()

    return ser

def getSetting(file):
    with open(file,"r") as keyFile:
        key = keyFile.readline()
        return key.rstrip()

ser = getSerial()
key = getSetting("key")
endpoint = getSetting("endpoint")

try:
    ser.flushInput()
    ser.flushOutput()

    while True:
        line = ser.readline();
        try:
            parsed = json.loads(line)
            url = endpoint + "?apiKey=" + key
            print(url)
            response = requests.post(url, json=parsed)
            print(response.status_code, response.reason)
            if response.status_code == 200:
                data = response.json()
                if data.drain == True:
                    ser.writeline("d")
        except Exception, e:
            print(str(e))
except Exception, e:
    print(str(e))

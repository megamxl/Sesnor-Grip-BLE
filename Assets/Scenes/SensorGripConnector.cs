using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SensorGripConnector : MonoBehaviour
{
    public Text deviceScanButtonText;
    public Text deviceScanStatusText;
    public Text serviceScanStatusText;
    public Text characteristicScanStatusText;

    public bool isScanningDevices;
    public bool isScanningServices;
    public bool isScanningCharacteristics;
    public bool isSubscribed;

    public Button serviceScanButton;
    public Button characteristicScanButton;

    public GameObject deviceScanResultProto;

    public string selectedDeviceId;
    public string selectedServiceId;
    public string selectedCharacteristicId;
    public Button subscribeButton;

    public Text subcribeText;


    public Dropdown serviceDropdown;
    public Dropdown characteristicDropdown;
    private readonly Dictionary<string, string> characteristicNames = new();

    private readonly Dictionary<string, Dictionary<string, string>> devices = new();
    private string lastError;

    private Transform scanResultRoot;

    
    public static SensorGripConnector Instance { get; private set; }
    
    public event Action<SensorData> OnDataChanged;
    
    private SensorData currentData;


    private void Awake()
    {
        // Ensure only one instance of the manager exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        scanResultRoot = deviceScanResultProto.transform.parent;
        deviceScanResultProto.transform.SetParent(null);
    }


    private void Update()
    {
        if (isScanningServices) ScanForServicesLoop();
        if (isScanningDevices) ScanningForDevicesLoop();
        if (isScanningCharacteristics) SanForCharacteristicsLoop();
        if (isSubscribed) SubscribeToDataLoop();
    }

    public void StartStopDeviceScan()
    {
        if (!isScanningDevices)
        {
            // start new scan
            for (var i = scanResultRoot.childCount - 1; i >= 0; i--)
                Destroy(scanResultRoot.GetChild(i).gameObject);
            BleApi.StartDeviceScan();
            isScanningDevices = true;
            deviceScanButtonText.text = "Stop scan";
            deviceScanStatusText.text = "scanning";
        }
        else
        {
            // stop scan
            isScanningDevices = false;
            BleApi.StopDeviceScan();
            deviceScanButtonText.text = "Start scan";
            deviceScanStatusText.text = "stopped";
        }
    }


    private void ScanningForDevicesLoop()
    {
        BleApi.ScanStatus status;
        var res = new BleApi.DeviceUpdate();
        do
        {
            status = BleApi.PollDevice(ref res, false);
            if (status == BleApi.ScanStatus.AVAILABLE)
            {
                if (!devices.ContainsKey(res.id))
                    devices[res.id] = new Dictionary<string, string>
                    {
                        { "name", "" },
                        { "isConnectable", "False" }
                    };
                if (res.nameUpdated)
                    devices[res.id]["name"] = res.name;
                if (res.isConnectableUpdated)
                    devices[res.id]["isConnectable"] = res.isConnectable.ToString();
                // consider only devices which have a name and which are connectable
                //TODO talk about this if nadien want this 
                if (devices[res.id]["name"] != "" && devices[res.id]["isConnectable"] == "True" &&
                    devices[res.id]["name"].ToLower().Contains("senso"))
                {
                    Debug.Log(devices[res.id]["name"]);

                    var g = Instantiate(deviceScanResultProto, scanResultRoot);
                    g.name = res.id;
                    g.transform.GetChild(0).GetComponent<Text>().text = devices[res.id]["name"];
                    g.transform.GetChild(1).GetComponent<Text>().text = res.id;
                }
            }
            else if (status == BleApi.ScanStatus.FINISHED)
            {
                isScanningDevices = false;
                deviceScanButtonText.text = "Scan devices";
                deviceScanStatusText.text = "finished";
            }
        } while (status == BleApi.ScanStatus.AVAILABLE);
    }

    public void SelectDevice(GameObject data)
    {
        for (var i = 0; i < scanResultRoot.transform.childCount; i++)
        {
            var child = scanResultRoot.transform.GetChild(i).gameObject;
            child.transform.GetChild(0).GetComponent<Text>().color = child == data
                ? Color.red
                : deviceScanResultProto.transform.GetChild(0).GetComponent<Text>().color;
        }

        selectedDeviceId = data.name;
        //StartServiceScan();
        Subscribe();
    }


    public void StartServiceScan()
    {
        if (!isScanningServices)
        {
            // start new scan
            serviceDropdown.ClearOptions();
            BleApi.ScanServices(selectedDeviceId);
            isScanningServices = true;
        }
    }

    private void ScanForServicesLoop()
    {
        BleApi.ScanStatus status;
        var res = new BleApi.Service();
        do
        {
            status = BleApi.PollService(out res, false);
            if (status == BleApi.ScanStatus.AVAILABLE)
            {
                if (res.uuid.Contains("1111-"))
                {
                    serviceDropdown.AddOptions(new List<string> { res.uuid });
                    // first option gets selected
                    if (serviceDropdown.options.Count == 1)
                        SelectService(serviceDropdown.gameObject);
                }
            }
            else if (status == BleApi.ScanStatus.FINISHED)
            {
                isScanningServices = false;
            }
        } while (status == BleApi.ScanStatus.AVAILABLE);
    }


    public void SelectService(GameObject data)
    {
        selectedServiceId = serviceDropdown.options[serviceDropdown.value].text;
        characteristicScanButton.interactable = true;
    }


    private void SanForCharacteristicsLoop()
    {
        BleApi.ScanStatus status;
        var res = new BleApi.Characteristic();
        do
        {
            status = BleApi.PollCharacteristic(out res, false);
            if (status == BleApi.ScanStatus.AVAILABLE)
            {
                var name = res.userDescription != "no description available" ? res.userDescription : res.uuid;
                characteristicNames[name] = res.uuid;
                Debug.Log(res.uuid);
                if (res.uuid.ToLower().Contains("3004"))
                {
                    characteristicDropdown.AddOptions(new List<string> { name });
                    // first option gets selected
                    if (characteristicDropdown.options.Count == 1)
                        SelectCharacteristic(characteristicDropdown.gameObject);
                    //isScanningCharacteristics = false;
                    //break;
                }
            }
            else if (status == BleApi.ScanStatus.FINISHED)
            {
                isScanningCharacteristics = false;
                characteristicScanButton.interactable = true;
            }
        } while (status == BleApi.ScanStatus.AVAILABLE);
    }

    public void StartCharacteristicScan()
    {
        if (!isScanningCharacteristics)
        {
            // start new scan
            characteristicDropdown.ClearOptions();
            BleApi.ScanCharacteristics(selectedDeviceId, selectedServiceId);
            isScanningCharacteristics = true;
            characteristicScanButton.interactable = false;
        }
    }

    public void SelectCharacteristic(GameObject data)
    {
        var name = characteristicDropdown.options[characteristicDropdown.value].text;
        selectedCharacteristicId = characteristicNames[name];
    }


    public void Subscribe()
    {
        // no error code available in non-blocking mode
        //                                                       00001111-0000-1000-8000-00805f9b34fb
        BleApi.SubscribeCharacteristic(selectedDeviceId, "{00001111-0000-1000-8000-00805f9b34fb}",
            "{00003004-0000-1000-8000-00805f9b34fb}", false);
        isSubscribed = true;
    }

    private void SubscribeToDataLoop()
    {
        var res = new BleApi.BLEData();
        while (BleApi.PollData(out res, false))
        {
            var length = res.buf.Length;

            var data = res.buf;
            
            //subcribeText.text = BitConverter.ToString(res.buf, 0, res.size);


            var sensorData = SensorData.FromByteArray(res.buf);
            
            UpdateData(sensorData);
            
            //Debug.Log(sensorData.TipSensorValue);

            subcribeText.text = sensorData.ToString();
                //subcribeText.text = Encoding.ASCII.GetString(res.buf, 0, res.size);
            //FormatSensorGripDataToDictionary(data);
        }
    }
    
    private void UpdateData(SensorData newData)
    {
        if (newData != currentData)
        {
            currentData = newData;
            OnDataChanged?.Invoke(currentData); // Notify subscribers
        }
    }


    private static void FormatSensorGripDataToDictionary(byte[] data)
    {
        var meassurments = new Dictionary<string, string>
        {
            { "timestamp", BitConverter.ToUInt16(data, 0).ToString() },
            { "tipSensorValue", BitConverter.ToInt16(data, 2).ToString() },
            { "fingerSensorValue", BitConverter.ToInt16(data, 4).ToString() },
            { "angle", Math.Abs(BitConverter.ToInt16(data, 6)).ToString() },
            { "speed", BitConverter.ToInt16(data, 2) > 15 ? BitConverter.ToInt16(data, 8).ToString() : "0" },
            { "batteryLevel", BitConverter.ToInt16(data, 10).ToString() },
            { "secondsInRange", BitConverter.ToInt16(data, 12).ToString() },
            { "secondsInUse", BitConverter.ToInt16(data, 14).ToString() },
            { "tipSensorUpperRange", BitConverter.ToInt16(data, 16).ToString() },
            { "tipSensorLowerRange", BitConverter.ToInt16(data, 18).ToString() },
            { "fingerSensorUpperRange", BitConverter.ToInt16(data, 20).ToString() },
            { "fingerSensorLowerRange", BitConverter.ToInt16(data, 22).ToString() },
            { "accX", BitConverter.ToSingle(data, 24).ToString() },
            { "accY", BitConverter.ToSingle(data, 28).ToString() },
            { "accZ", BitConverter.ToSingle(data, 32).ToString() },
            { "gyroX", BitConverter.ToSingle(data, 36).ToString() },
            { "gyroY", BitConverter.ToSingle(data, 40).ToString() },
            { "gyroZ", BitConverter.ToSingle(data, 44).ToString() }
        };

        var s = "\n";

        foreach (var kvp in meassurments) s = s + "Key = " + kvp.Key + "Value = " + kvp.Value + "\n";
        //textBox3.Text += ("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
        //Debug.Log(s);                 

        Debug.Log("tipSensorValue " + BitConverter.ToInt16(data, 2));
    }

    public class SensorData
    {
        // Define the structure of your Bluetooth data here
        public ushort Timestamp { get; set; }
        public short TipSensorValue { get; set; }
        public short FingerSensorValue { get; set; }
        public ushort Angle { get; set; }
        public short Speed { get; set; }
        public short BatteryLevel { get; set; }
        public short SecondsInRange { get; set; }
        public short SecondsInUse { get; set; }
        public short TipSensorUpperRange { get; set; }
        public short TipSensorLowerRange { get; set; }
        public short FingerSensorUpperRange { get; set; }
        public short FingerSensorLowerRange { get; set; }
        public float AccX { get; set; }
        public float AccY { get; set; }
        public float AccZ { get; set; }
        public float GyroX { get; set; }
        public float GyroY { get; set; }
        public float GyroZ { get; set; }

        public static SensorData FromByteArray(byte[] data)
        {
            if (data == null || data.Length < 48) Debug.Log("Invalid data array");

            return new SensorData
            {
                Timestamp = BitConverter.ToUInt16(data, 0),
                TipSensorValue = BitConverter.ToInt16(data, 2),
                FingerSensorValue = BitConverter.ToInt16(data, 4),
                Angle = (ushort)Math.Abs(BitConverter.ToInt16(data, 6)),
                Speed = BitConverter.ToInt16(data, 2) > 15 ? BitConverter.ToInt16(data, 8) : (short)0,
                BatteryLevel = BitConverter.ToInt16(data, 10),
                SecondsInRange = BitConverter.ToInt16(data, 12),
                SecondsInUse = BitConverter.ToInt16(data, 14),
                TipSensorUpperRange = BitConverter.ToInt16(data, 16),
                TipSensorLowerRange = BitConverter.ToInt16(data, 18),
                FingerSensorUpperRange = BitConverter.ToInt16(data, 20),
                FingerSensorLowerRange = BitConverter.ToInt16(data, 22),
                AccX = BitConverter.ToSingle(data, 24),
                AccY = BitConverter.ToSingle(data, 28),
                AccZ = BitConverter.ToSingle(data, 32),
                GyroX = BitConverter.ToSingle(data, 36),
                GyroY = BitConverter.ToSingle(data, 40),
                GyroZ = BitConverter.ToSingle(data, 44)
            };
        }
        
        public override string ToString()
        {
            return $"Timestamp: {Timestamp}, \n" +
                   $"TipSensorValue: {TipSensorValue}, \n" +
                   $"FingerSensorValue: {FingerSensorValue}, \n" +
                   $"Angle: {Angle}, \n" +
                   $"Speed: {Speed}, \n " +
                   $"BatteryLevel: {BatteryLevel}, \n" +
                   $"SecondsInRange: {SecondsInRange}, \n" +
                   $"SecondsInUse: {SecondsInUse}, \n" +
                   $"TipSensorUpperRange: {TipSensorUpperRange}, \n" +
                   $"TipSensorLowerRange: {TipSensorLowerRange}, \n" +
                   $"FingerSensorUpperRange: {FingerSensorUpperRange}, \n" +
                   $"FingerSensorLowerRange: {FingerSensorLowerRange}, \n" +
                   $"AccX: {AccX}, \n" +
                   $"AccY: {AccY}, \n" +
                   $"AccZ: {AccZ}, \n" +
                   $"GyroX: {GyroX}, \n" +
                   $"GyroY: {GyroY}, \n" +
                   $"GyroZ: {GyroZ}\n";
        }
    }

    public void QuitBel()
    {
        BleApi.Quit();
    }
}
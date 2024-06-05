using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class Demo : MonoBehaviour
{
    public bool isScanningDevices;
    public bool isScanningServices;
    public bool isScanningCharacteristics;
    public bool isSubscribed;
    public Text deviceScanButtonText;
    public Text deviceScanStatusText;
    public GameObject deviceScanResultProto;
    public Button serviceScanButton;
    public Text serviceScanStatusText;
    public Dropdown serviceDropdown;
    public Button characteristicScanButton;
    public Text characteristicScanStatusText;
    public Dropdown characteristicDropdown;
    public Button subscribeButton;
    public Text subcribeText;
    public Button writeButton;
    public InputField writeInput;
    public Text errorText;
    public string selectedDeviceId;
    public string selectedServiceId;
    public string selectedCharacteristicId;
    private readonly Dictionary<string, string> characteristicNames = new();
    private readonly Dictionary<string, Dictionary<string, string>> devices = new();
    private string lastError;

    private Transform scanResultRoot;

    // Start is called before the first frame update
    private void Start()
    {
        scanResultRoot = deviceScanResultProto.transform.parent;
        deviceScanResultProto.transform.SetParent(null);
    }

    // Update is called once per frame
    private void Update()
    {
        if (isScanningDevices) ScanningForDevicesLoop();
        if (isScanningServices) ScanForServicesLoop();
        if (isScanningCharacteristics) SanForCharacteristicsLoop();
        if (isSubscribed) SubscribeToDataLoop();
        {
            // log potential errors
            var res = new BleApi.ErrorMessage();
            BleApi.GetError(out res);
            if (lastError != res.msg)
            {
                Debug.LogError(res.msg);
                errorText.text = res.msg;
                lastError = res.msg;
            }
        }
    }

    private void OnApplicationQuit()
    {
        BleApi.Quit();
    }

    private void SubscribeToDataLoop()
    {
        var res = new BleApi.BLEData();
        while (BleApi.PollData(out res, false))
        {
            subcribeText.text = BitConverter.ToString(res.buf, 0, res.size);
            //subcribeText.text = Encoding.ASCII.GetString(res.buf, 0, res.size);

            var length = res.buf.Length;

            var data = res.buf;

            FormatSensorGripDataToDictionary(data);
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
                characteristicDropdown.AddOptions(new List<string> { name });
                // first option gets selected
                if (characteristicDropdown.options.Count == 1)
                    SelectCharacteristic(characteristicDropdown.gameObject);
            }
            else if (status == BleApi.ScanStatus.FINISHED)
            {
                isScanningCharacteristics = false;
                characteristicScanButton.interactable = true;
                characteristicScanStatusText.text = "finished";
            }
        } while (status == BleApi.ScanStatus.AVAILABLE);
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
                serviceDropdown.AddOptions(new List<string> { res.uuid });
                // first option gets selected
                if (serviceDropdown.options.Count == 1)
                    SelectService(serviceDropdown.gameObject);
            }
            else if (status == BleApi.ScanStatus.FINISHED)
            {
                isScanningServices = false;
                serviceScanButton.interactable = true;
                serviceScanStatusText.text = "finished";
            }
        } while (status == BleApi.ScanStatus.AVAILABLE);
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
                if (devices[res.id]["name"] != "" && devices[res.id]["isConnectable"] == "True")
                {
                    // add new device to list
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
        serviceScanButton.interactable = true;
    }

    public void StartServiceScan()
    {
        if (!isScanningServices)
        {
            // start new scan
            serviceDropdown.ClearOptions();
            BleApi.ScanServices(selectedDeviceId);
            isScanningServices = true;
            serviceScanStatusText.text = "scanning";
            serviceScanButton.interactable = false;
        }
    }

    public void SelectService(GameObject data)
    {
        selectedServiceId = serviceDropdown.options[serviceDropdown.value].text;
        characteristicScanButton.interactable = true;
    }

    public void StartCharacteristicScan()
    {
        if (!isScanningCharacteristics)
        {
            // start new scan
            characteristicDropdown.ClearOptions();
            BleApi.ScanCharacteristics(selectedDeviceId, selectedServiceId);
            isScanningCharacteristics = true;
            characteristicScanStatusText.text = "scanning";
            characteristicScanButton.interactable = false;
        }
    }

    public void SelectCharacteristic(GameObject data)
    {
        var name = characteristicDropdown.options[characteristicDropdown.value].text;
        selectedCharacteristicId = characteristicNames[name];
        subscribeButton.interactable = true;
        writeButton.interactable = true;
    }

    public void Subscribe()
    {
        // no error code available in non-blocking mode
        BleApi.SubscribeCharacteristic(selectedDeviceId, selectedServiceId, selectedCharacteristicId, false);
        isSubscribed = true;
    }

    public void Write()
    {
        var payload = Encoding.ASCII.GetBytes(writeInput.text);
        var data = new BleApi.BLEData();
        data.buf = new byte[512];
        data.size = (short)payload.Length;
        data.deviceId = selectedDeviceId;
        data.serviceUuid = selectedServiceId;
        data.characteristicUuid = selectedCharacteristicId;
        for (var i = 0; i < payload.Length; i++)
            data.buf[i] = payload[i];
        // no error code available in non-blocking mode
        BleApi.SendData(in data, false);
    }
}
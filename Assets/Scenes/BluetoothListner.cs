using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BluetoothListner : MonoBehaviour
{

    private bool isFirst = true;
    
    private void OnEnable()
    {
        //SensorGripConnector.Instance.OnDataChanged += HandleDataChanged;
    }

    private void OnDisable()
    {
        SensorGripConnector.Instance.OnDataChanged -= HandleDataChanged;
    }

    private void HandleDataChanged(SensorGripConnector.SensorData data)
    {
        // Handle the updated data
        Debug.Log("Received new Bluetooth data: " + data.TipSensorValue);
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (SensorGripConnector.Instance)
        {
            if (isFirst)
            {
                SensorGripConnector.Instance.OnDataChanged += HandleDataChanged;

                isFirst = false;
                
            }
        }
    }
}

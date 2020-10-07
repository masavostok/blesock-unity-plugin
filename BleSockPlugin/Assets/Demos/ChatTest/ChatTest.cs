using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ChatTest : MonoBehaviour
{
    public GameObject modeSelectObject;
    public InputField playerNameInputField;
    public Button peripheralButton;
    public Button centralButton;

    public GameObject peripheralObject;
    public Button advertiseButton;
    public Button stopButton;

    public GameObject centralObject;
    public Dropdown devicesDropdown;
    public Button connectButton;
    public Button disconnectButton;

    public GameObject chatObject;
    public Text statusText;
    public Text logsText;
    public InputField sendInputField;
    public Button backButton;

    [SerializeField] private Text hostName;
    [SerializeField] private Text userName;
    [SerializeField] private Text fatalError;

    private const string PROTOCOL_IDENTIFIER = "BleRemoconComm";


    private class DeviceOptionData : Dropdown.OptionData
    {
        public int deviceId { get; private set; }

        public DeviceOptionData(string deviceName, int deviceId)
        {
            text = deviceName;
            this.deviceId = deviceId;
        }
    }


    //private BleSock.PeerBase mPeer;
    private BleSock.RemoconPeer peerRemocon;
    private BleSock.BaseUnitPeer peerBaseUnit;
    private int session = 0;
    private List<string> mLogs = new List<string>();

    private void OnEnable()
    {
    }

    private void Start()
    {
        playerNameInputField.text = "User_" + UnityEngine.Random.Range(100,9999);

        modeSelectObject.SetActive(true);

        playerNameInputField.onEndEdit.AddListener((name) =>
        {
            bool interactable = !string.IsNullOrEmpty(name);
            peripheralButton.interactable = interactable;
            centralButton.interactable = interactable;
        });

        // Peripheral
        // リモコン側、開始通知（アドバタイズ）、接続受け入れ、リモコンコマンド

        peripheralObject.SetActive(false);

        //hostButton.interactable = false;
        peripheralButton.GetComponent<Selectable>().Select();
        peripheralButton.onClick.AddListener(() =>
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!BleSock.AndroidUtils.IsPeripheralAvailable)
            {
                Log("このハードウェアはBluetooth LEの機能が不足しています");
                fatalError.text = "※このハードウェアはBluetooth LE ペリフェラルをサポートしていないため動作しません。";
                return;
            }
#endif
            modeSelectObject.SetActive(false);
            peripheralObject.SetActive(true);
            chatObject.SetActive(true);

            advertiseButton.interactable = false;
            stopButton.interactable = false;

            backButton.interactable = true;

            var remocon = new BleSock.RemoconPeer();
            
            remocon.onReady += () =>
            {
                Log("初期化が完了しました");
                advertiseButton.interactable = true;
                sendInputField.interactable = true;
                advertiseButton.onClick.Invoke();
                stopButton.GetComponent<Selectable>().Select();
            };

            remocon.onBluetoothRequire += () =>
            {
                Log("Bluetoothを有効にしてください");
            };

            remocon.onFail += () =>
            {
                Log("失敗しました");
            };

            remocon.onPlayerJoin += (player) =>
            {
                Log("{0} が参加しました", player.PlayerName);
            };

            remocon.onPlayerLeave += (player) =>
            {
                Log("{0} が離脱しました", player.PlayerName);
            };

            remocon.onReceive += (message, messageSize, sender) =>
            {
                Log("{0}: {1}", sender.PlayerName, Encoding.UTF8.GetString(message, 0, messageSize));
            };

            try
            {
                remocon.Initialize(PROTOCOL_IDENTIFIER, playerNameInputField.text);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Log("初期化できません");
                return;
            }

            Log("初期化しています..");
            peerRemocon = remocon;
        });

        advertiseButton.onClick.AddListener(() =>
        {            
            try
            {
                ((BleSock.RemoconPeer)peerRemocon).StartAdvertising(playerNameInputField.text);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Log("アドバタイズ開始できません");
                return;
            }

            Log("本体の接続を待っています..");
            advertiseButton.interactable = false;
            stopButton.interactable = true;
            hostName.text = playerNameInputField.text;
            stopButton.GetComponent<Selectable>().Select();
        });

        stopButton.onClick.AddListener(() =>
        {
            ((BleSock.RemoconPeer)peerRemocon).StopAdvertising();

            Log("アドバタイズを停止しました");
            advertiseButton.interactable = true;
            stopButton.interactable = false;
            advertiseButton.GetComponent<Selectable>().Select();
        });

        //peerRemocon.Send(bytes, bytes.Length, BleSock.Address.All);

        /////////////////////////////////////////////////////////////////////////////////

        // Central
        // 本体側、リモコンを探す（アドバタイズスキャン）、接続トライ

        centralObject.SetActive(false);

        //joinButton.interactable = false;
        centralButton.onClick.AddListener(() =>
        {
            modeSelectObject.SetActive(false);
            centralObject.SetActive(true);
            chatObject.SetActive(true);

            devicesDropdown.interactable = false;
            connectButton.interactable = false;
            disconnectButton.interactable = false;

            backButton.interactable = true;

            userName.text = playerNameInputField.text;

            var sbUnit = new BleSock.BaseUnitPeer();
            sbUnit.session = session++;

            sbUnit.onBluetoothRequire += () =>
            {
                Log("Bluetoothを有効にしてください");
            };

            sbUnit.onReady += () =>
            {
                Log("初期化が完了しました");

                try
                {
                    sbUnit.StartScan();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Log("スキャン開始できません");
                    return;
                }

                Log("リモコンを探索しています..");
                devicesDropdown.ClearOptions();
            };

            sbUnit.onFail += () =>
            {
                Log("失敗しました");
            };

            sbUnit.onDiscover += (deviceName, deviceId) =>
            {
                Log("リモコンを発見: {0} [{1}]", deviceName, deviceId);
                devicesDropdown.options.Add(new DeviceOptionData(deviceName, deviceId));

                if (!devicesDropdown.interactable)
                {
                    devicesDropdown.interactable = true;
                    devicesDropdown.value = 0;
                    devicesDropdown.RefreshShownValue();
                    connectButton.interactable = true;
                    connectButton.GetComponent<Selectable>().Select();
                }
            };

            sbUnit.onConnect += () =>
            {
                Log("接続されました");

                foreach (var player in sbUnit.Players)
                {
                    Log(player.PlayerName);
                }

                sendInputField.interactable = true;
            };

            sbUnit.onDisconnect += () =>
            {
                Log("切断されました");
                sendInputField.interactable = false;
                disconnectButton.interactable = false;
            };

            sbUnit.onPlayerJoin += (player) =>
            {
                Log("{0} が参加しました", player.PlayerName);
            };

            sbUnit.onPlayerLeave += (player) =>
            {
                Log("{0} が離脱しました", player.PlayerName);
            };

            sbUnit.onReceive += (message, messageSize, sender) =>
            {
                Log("{0}: {1}", sender.PlayerName, Encoding.UTF8.GetString(message, 0, messageSize));
            };

            try
            {
                sbUnit.Initialize(PROTOCOL_IDENTIFIER, playerNameInputField.text);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Log("初期化できません");
                return;
            }

            Log("初期化しています..");
            peerBaseUnit = sbUnit;
            AddNewCentralSession(sbUnit);
        });

        connectButton.onClick.AddListener(() =>
        {
            var optionData = (DeviceOptionData)devicesDropdown.options[devicesDropdown.value];
            try
            {
                ((BleSock.BaseUnitPeer)peerBaseUnit).Connect(optionData.deviceId);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Log("エラー");
                return;
            }

            Log("デバイスに接続しています..");
            devicesDropdown.interactable = false;
            connectButton.interactable = false;
            disconnectButton.interactable = true;
        });

        disconnectButton.onClick.AddListener(() =>
        {
            ((BleSock.BaseUnitPeer)peerBaseUnit).Disconnect();
        });

        /////////////////////////////////////////////////////////////////////////////////
        // Chat

        chatObject.SetActive(false);

        sendInputField.interactable = false;
        sendInputField.onEndEdit.AddListener((text) =>
        {
            BleSock.PeerBase peer = peerBaseUnit != null ? (BleSock.PeerBase)peerBaseUnit : (BleSock.PeerBase)peerRemocon;
            var bytes = Encoding.UTF8.GetBytes(text);
            try
            {
                peer.Send(bytes, bytes.Length, BleSock.Address.Others);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Log("エラー");
                return;
            }

            sendInputField.text = "";
        });

        backButton.interactable = false;
        backButton.onClick.AddListener(() =>
        {
            if (peerBaseUnit != null)
            {
                peerBaseUnit.Dispose();
                peerBaseUnit = null;
            }
            if (peerRemocon != null)
            {
                peerRemocon.Dispose();
                peerRemocon = null;
            }

            mLogs.Clear();

            modeSelectObject.SetActive(true);
            peripheralObject.SetActive(false);
            centralObject.SetActive(false);
            chatObject.SetActive(false);
            peripheralButton.GetComponent<Selectable>().Select();
        });
    }

    private void AddNewCentralSession(BleSock.BaseUnitPeer peer)
    {
        int no = session;
        var p = peer;
    }

    private void Update()
    {
        if (peerRemocon != null)
        {
            statusText.text = string.Format("BluetoothEnabled: {0}", peerRemocon.IsBluetoothEnabled.ToString());
        }
    }

    private void OnDestroy()
    {
        if (peerBaseUnit != null)
        {
            peerBaseUnit.Dispose();
            peerBaseUnit = null;
        }
        if (peerRemocon != null)
        {
            peerRemocon.Dispose();
            peerRemocon = null;
        }
    }

    private void Log(string format, params object[] args)
    {
        mLogs.Add(string.Format(format, args));

        if (mLogs.Count > 10)
        {
            mLogs.RemoveAt(0);
        }

        var builder = new StringBuilder();
        foreach (var log in mLogs)
        {
            builder.AppendLine(log);
        }

        logsText.text = builder.ToString();
    }

    public void OnClickExit()
    {
        Application.Quit();
    }
}

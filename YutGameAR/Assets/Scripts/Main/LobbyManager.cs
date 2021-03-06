using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Main
{
    public class LobbyManager : MonoBehaviour
    {
        public string roomName;     // create room menu -> (roomName) -> lobby 
        public string leaderName;   // find room menu -> (leaderName) -> lobby
        public GameObject manomotionGroup;
        public GameObject yutBoard;
        public GameObject yutPlate;
        public Canvas ingameCanvas;
        private struct AbsenceOfLeader
        {
            public bool onAbsence;
        }

        private struct RoomName
        {
            public string roomName;
        }

        private TextMeshPro _redUserName;
        private TextMeshPro _blueUserName;
        private TextMeshProUGUI _notification;
        private Button _leaveRoomBtn;
        private Button _gameStartBtn;

        private int _notificationTimer;
        private bool _isRoomLeader;

        void Init()
        {
            Transform cvsTransform = transform.Find("LobbyCanvas");
            _redUserName = transform.Find("TeamRed").Find("UserNameTMP").GetComponent<TextMeshPro>();
            _blueUserName = transform.Find("TeamBlue").Find("UserNameTMP").GetComponent<TextMeshPro>();
            _notification = cvsTransform.Find("notification").GetComponent<TextMeshProUGUI>();
            _gameStartBtn = cvsTransform.Find("GameStartBtn").GetComponent<Button>();
            _leaveRoomBtn = cvsTransform.Find("LeaveRoomBtn").GetComponent<Button>();

            _gameStartBtn.onClick.AddListener(delegate { OnGameStartBtnClick(); });
            _leaveRoomBtn.onClick.AddListener(delegate { OnLeaveRoomBtnClick(); });
            StartCoroutine(NotificationTimer());
            StartCoroutine(RegisterSocketIOEvent());
        }
        
        void Awake()
        {
            Init();
        }

        void Start()
        {
            
            if (_isRoomLeader)
            {
                _redUserName.text = NetworkCore.Instance.UserData.userNickName;
            }
            else
            {
                _redUserName.text = leaderName;
                _blueUserName.text = NetworkCore.Instance.UserData.userNickName;   
            }
        }
        
        public void SetIsRoomLeader(bool isRoomLeader)
        {
            _isRoomLeader = isRoomLeader;
        }

        void OnGameStartBtnClick()
        {
            _notificationTimer = 0;
            if (_redUserName.text.Equals("") || _blueUserName.text.Equals(""))
            {
                _notification.text = "You need an opponent to play the game.";
                return;
            }
            if (_isRoomLeader)
            {
                if (FMSocketIOManager.instance != null)
                {
                    if (FMSocketIOManager.instance.Ready)
                    {
                        RoomName rName = new RoomName();
                        rName.roomName = roomName;
                        FMSocketIOManager.instance.Emit("Event_GameStart", JsonUtility.ToJson(rName));
                    }
                    else { _notification.text = "Cannot connect to server"; }
                }
                else { _notification.text = "Socket IO Object is null"; }
            }
            else { _notification.text = "You're not a room master."; }
        }

        void OnLeaveRoomBtnClick()
        {
            if (FMSocketIOManager.instance != null)
            {
                if (FMSocketIOManager.instance.Ready)
                {
                    AbsenceOfLeader absenceOfLeader = new AbsenceOfLeader();
                    if (_isRoomLeader)
                    {
                        absenceOfLeader.onAbsence = true;
                    }
                    else
                    {
                        absenceOfLeader.onAbsence = false;
                    }

                    FMSocketIOManager.instance.Emit("Event_LeaveRoom", JsonUtility.ToJson(absenceOfLeader));
                }
                else
                {
                    _notification.text = "Cannot connect to server";
                }
            }
            else
            {
                _notification.text = "Socket IO Object is null";
            }
        }

        IEnumerator NotificationTimer()
        {
            while (true)
            {
                _notificationTimer += 1;
                if (_notificationTimer >= 90)
                {
                    _notification.text = "";
                    _notificationTimer = 0;
                }

                yield return null;
            }
        }

        IEnumerator RegisterSocketIOEvent()
        {
            while (FMSocketIOManager.instance == null)
                yield return null;

            while (!FMSocketIOManager.instance.Ready)
                yield return null;

            FMSocketIOManager.instance.On("Event_GameStart_Result", (e) =>
            {
                string data = e.data.Substring(1, e.data.Length - 2);
                switch (data)
                {
                    case "Success":
                        manomotionGroup.SetActive(true);
                        yutBoard.SetActive(true);
                        yutPlate.SetActive(true);
                        ingameCanvas.gameObject.SetActive(true);
                        yutBoard.transform.Find("YutGameManager").GetComponent<YutGameManager>().rName = roomName;
                        if (_isRoomLeader)
                        {
                            yutBoard.transform.Find("YutGameManager").GetComponent<YutGameManager>().userColor = "Red";
                            yutBoard.transform.Find("YutGameManager").GetComponent<YutGameManager>().MyTurn = true;
                        }
                        else
                            yutBoard.transform.Find("YutGameManager").GetComponent<YutGameManager>().userColor = "Blue";
                        yutBoard.transform.Find("YutGameManager").GetComponent<YutGameManager>().RedName = _redUserName.text;
                        yutBoard.transform.Find("YutGameManager").GetComponent<YutGameManager>().BlueName = _blueUserName.text;
                        yutBoard.transform.position = ARPlaneInfo.Instance.center + new Vector3(-0.3f, 0, 0.2f);
                        yutPlate.transform.position = ARPlaneInfo.Instance.center + new Vector3(0.3f, 0, 0.2f);
                        gameObject.SetActive(false);
                        StopCoroutine(NotificationTimer());
                        break;
                    case "Fail":
                        _notificationTimer = 0;
                        _notification.text = "Fail to start game. An error has occurred.";
                        break;
                }
            });

            FMSocketIOManager.instance.On("Event_LeaveRoom_Result", (e) =>
            {
                string data = e.data.Substring(1, e.data.Length - 2);

                switch (data)
                {
                    case "Success":
                        break;
                    case "Fail":
                        _notificationTimer = 0;
                        _notification.text = "Fail to leave room. An error has occurred.";
                        break;
                }
            });

            FMSocketIOManager.instance.On("Event_UserJoin", (e) =>
            {
                string data = e.data.Substring(1, e.data.Length - 2);
                _blueUserName.text = data;
            });
            
            FMSocketIOManager.instance.On("Event_UserLeave", (e) =>
            {
                _blueUserName.text = "Empty";
            });
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using TMPro;

public class YutGameManager : MonoBehaviour
{
    
    
    public string rName;
    private bool _select = false;
    private GameObject _selectedPiece;
    private Dictionary<string, YutTree.TreeNode> _enableNode;

    private int BlueScore = 0;
    private int RedScore = 0;
    private YutThrow YutComponent;
    private YutTree TreeComponent;
    private GameObject[] PiecesSet;
    private int _notificationTimer;
    private AudioSource _audioSource;

    public static YutGameManager YutManager;
    public string userColor;
    public bool MyTurn;
    public string RedName, BlueName;
    public Canvas InGameCanvas;
    public AudioClip GoalInClip;

    private struct UserInfo
    {
        
        public string throughPos;
        public string endPos;
        public string roomName;
        public int id;
    }

    private struct UserTurn
    {
        public bool userTurn;
        public string roomName;
    }
    
    public bool Select { get { return _select; } }

    void Awake()
    {
        YutManager = this;
        YutComponent = GetComponent<YutThrow>();
        TreeComponent = GetComponent<YutTree>();
        PiecesSet = GameObject.FindGameObjectsWithTag("Piece");
        _audioSource = GetComponent<AudioSource>();
        StartCoroutine(NotificationTimer());
        StartCoroutine(SocketIOEvent());
        
    }

    // Start is called before the first frame update
    void Start()
    {
        InGameCanvas.transform.Find("RedName").GetComponent<TextMeshProUGUI>().text = RedName;
        InGameCanvas.transform.Find("BlueName").GetComponent<TextMeshProUGUI>().text = BlueName;
    }

    // Update is called once per frame
    void Update()
    {
        selectAndMove();
    }

    void selectAndMove()
    {
        // 윷을 던진 상태에서 터치를 하는 경우
        if (Input.GetMouseButtonDown(0) && YutComponent.Throwing && MyTurn)
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            
            if (Physics.Raycast(ray, out hit))
            {
                // 터치하는 것이 말일 경우
                if (hit.collider.gameObject.CompareTag("Piece") && !_select && hit.collider.gameObject.GetComponent<Pieces>().teamColor.Equals(userColor))
                {
                    _select = true;
                    _selectedPiece = hit.collider.gameObject;
                    _enableNode = hit.collider.GetComponent<PathFinding>().PathFind(_selectedPiece, YutComponent.SelectNumber);
                }

                // 말을 터치한 후 갈 발판을 터치한 경우
                else if (hit.collider.gameObject.CompareTag("FootHold") && _select && _enableNode.ContainsKey(hit.collider.name))
                {
                    UserInfo info = new UserInfo();
                    UserTurn turn = new UserTurn();
                    info.roomName = rName;
                    turn.roomName = rName;
                    
                    // when the pieces didn't start
                    if (_enableNode[hit.collider.name] == null)
                    {
                        if (_selectedPiece.GetComponent<Pieces>().PosName == "FootHold_0") 
                        {
                            info.throughPos = "FootHold_29";
                            info.endPos = hit.collider.name;
                            info.id = _selectedPiece.GetComponent<Pieces>().ID;
                            string data = JsonUtility.ToJson(info);
                            FMSocketIOManager.instance.Emit("Event_SendPos", data);
                            StartCoroutine(MoveTo(_selectedPiece, TreeComponent.NodeName["FootHold_29"].FootHold.transform.position, hit.collider.transform.position, hit.collider.name)); 
                        }
                        else 
                        {
                            info.throughPos = " ";
                            info.endPos = hit.collider.name;
                            info.id = _selectedPiece.GetComponent<Pieces>().ID;
                            string data = JsonUtility.ToJson(info);
                            FMSocketIOManager.instance.Emit("Event_SendPos", data);
                            StartCoroutine(MoveTo(_selectedPiece, hit.collider.transform.position, hit.collider.name)); 
                        }
                    }
                    // else
                    else 
                    {
                        info.throughPos = _enableNode[hit.collider.name].FootHold.name;
                        info.endPos = hit.collider.name;
                        info.id = _selectedPiece.GetComponent<Pieces>().ID;
                        string data = JsonUtility.ToJson(info);
                        FMSocketIOManager.instance.Emit("Event_SendPos", data);
                        StartCoroutine(MoveTo(_selectedPiece, _enableNode[hit.collider.name].FootHold.transform.position, hit.collider.transform.position, hit.collider.name)); 
                    }
                    _selectedPiece.GetComponent<Pieces>().PosName = hit.collider.name;
                    _select = false;
                    DestroyArrow();

                    // 여러번 움직일 때
                    if(YutComponent.SelectNumber.Count > 1)
                    {
                        YutComponent.SelectNumber.Remove(TreeComponent.NodeName[hit.collider.name].Step);
                    }
                    else
                    {
                        YutComponent.Throwing = false;
                        YutComponent.SelectNumber.Clear();
                        MyTurn = false;
                        turn.userTurn = true;
                        string data = JsonUtility.ToJson(turn);
                        FMSocketIOManager.instance.Emit("Turn_Change", data);
                    }
                }

                // 그 외의 경우 초기로 돌려줌
                else
                {
                    DestroyArrow();
                    _select = false;
                }
            }
        }
    }

    //The pieces go to the same FootHold.
    //There are two cases when you are on the same team and when you are on a different team.
    void CatchPiece(string hitName, GameObject _selectedPiece)
    {
        
        for (int i = 0; i < PiecesSet.Length; i++)
        {

            if (hitName.Equals(PiecesSet[i].GetComponent<Pieces>().PosName) && !_selectedPiece.Equals(PiecesSet[i]))
            {
                Pieces ps = PiecesSet[i].GetComponent<Pieces>();
                if (_selectedPiece.GetComponent<Pieces>().teamColor.Equals(ps.teamColor))
                {
                    ps.Point += 1;
                    ps.SetRidingText("X" + ps.Point);
                    ps.HideText(true);
                    _selectedPiece.GetComponent<Pieces>().Point = 0;
                    _selectedPiece.GetComponent<Pieces>().PosName = "FootHold_0";
                    _selectedPiece.SetActive(false);
                    
                }
                else
                {
                    if (ps.Point > 1)
                    {
                        ps.PosName = "FootHold_0";
                        ps.transform.position = ps.InitPosition;
                        ps.Point = 1;
                        ps.HideText(false);

                        for (int j = 0; j < PiecesSet.Length; j++)
                        {
                            if (!PiecesSet[j].activeInHierarchy)
                            {
                                Pieces ps2 = PiecesSet[j].GetComponent<Pieces>();
                                if (ps2.teamColor.Equals(ps.teamColor) && ps2.Point == 0)
                                {
                                    PiecesSet[j].transform.position = ps2.InitPosition;
                                    ps2.Point = 1;
                                    PiecesSet[j].SetActive(true);
                                }
                            }

                        }
                    }
                    else
                    {
                        ps.PosName = "FootHold_0";
                        ps.transform.position = ps.InitPosition;

                    }
                    
                }
            }
        }
    }


    // Destroy the arrows when the piece is out of focus.
    void DestroyArrow()
    {
        GameObject[] arrow = GameObject.FindGameObjectsWithTag("Arrow");
        for(int i = 0; i< arrow.Length; i++)
            Destroy(arrow[i]);
    }

    void GoalInSetting(GameObject piece)
    {
        if (piece.GetComponent<Pieces>().teamColor.Equals("Red"))
        {
            RedScore += piece.GetComponent<Pieces>().Point;
            InGameCanvas.transform.Find("RedPoint").GetComponent<TextMeshProUGUI>().text = RedScore + " Pts";
        }
        else
        {
            BlueScore += piece.GetComponent<Pieces>().Point;
            InGameCanvas.transform.Find("BluePoint").GetComponent<TextMeshProUGUI>().text = BlueScore + " Pts";
        }
        piece.SetActive(false);
        _audioSource.PlayOneShot(GoalInClip);
        if(RedScore == 4)
        {
            if (userColor.Equals("Red"))
                InGameCanvas.transform.Find("Result").GetComponent<TextMeshProUGUI>().text = "WIN :)";
            else
                InGameCanvas.transform.Find("Result").GetComponent<TextMeshProUGUI>().text = "LOSE :(";
            StopCoroutine(NotificationTimer());
        }
        else if(BlueScore == 4)
        {
            if (userColor.Equals("Red"))
                InGameCanvas.transform.Find("Result").GetComponent<TextMeshProUGUI>().text = "LOSE :(";
            else
                InGameCanvas.transform.Find("Result").GetComponent<TextMeshProUGUI>().text = "WIN :)";
            StopCoroutine(NotificationTimer());
        }
    }

    IEnumerator MoveTo(GameObject piece, Vector3 throughPos, Vector3 toPos, string hitName)
    {

        float count = 0, count2 = 0;
        Vector3 wasPos = piece.transform.position;
        throughPos.y += 0.02f;
        toPos.y += 0.02f;
        while (true)
        {
            count += Time.deltaTime;
            piece.transform.position = Vector3.Lerp(wasPos, throughPos, count);
            if (piece.transform.position == throughPos)
            {
                count2 += Time.deltaTime;
                piece.transform.position = Vector3.Lerp(throughPos, toPos, count2);
                if (count2 >= 1)
                {
                    piece.transform.position = toPos;
                    CatchPiece(hitName, piece);

                    // the piece reached the finish line 
                    if (toPos.x == TreeComponent.NodeName["FootHold_30"].FootHold.transform.position.x)
                    {
                        GoalInSetting(piece);
                    }

                    break;
                }
            }
            yield return null;
        }
    }

    IEnumerator MoveTo(GameObject piece, Vector3 toPos, string hitName)
    {

        float count = 0;
        Vector3 wasPos = piece.transform.position;
        toPos.y += 0.02f;
        while (true)
        {
            count += Time.deltaTime;
            piece.transform.position = Vector3.Lerp(wasPos, toPos, count);
            if (count >= 1)
            {
                piece.transform.position = toPos;
                CatchPiece(hitName, piece);

                if (toPos.x == TreeComponent.NodeName["FootHold_30"].FootHold.transform.position.x)
                {
                    GoalInSetting(piece);
                }
                break;
            }
            yield return null;
        }
    }

    IEnumerator SocketIOEvent()
    {
        while (FMSocketIOManager.instance == null)
            yield return null;

        while (!FMSocketIOManager.instance.Ready)
            yield return null;

        FMSocketIOManager.instance.On("Event_SendPos_Result", (e) =>
        {
            
            UserInfo info = JsonUtility.FromJson<UserInfo>(e.data);
            GameObject piece = null;
            for (int i = 0; i < PiecesSet.Length; i++)
            {
                if(!PiecesSet[i].GetComponent<Pieces>().teamColor.Equals(userColor) && PiecesSet[i].GetComponent<Pieces>().ID == info.id)
                {
                    piece = PiecesSet[i];
                    break;
                }
            }

            if (info.throughPos != " ")
            {
                StartCoroutine(MoveTo(piece, TreeComponent.NodeName[info.throughPos].FootHold.transform.position, TreeComponent.NodeName[info.endPos].FootHold.transform.position, info.endPos));
            }
            else
            {
                StartCoroutine(MoveTo(piece, TreeComponent.NodeName[info.endPos].FootHold.transform.position, info.endPos));
            }
            piece.GetComponent<Pieces>().PosName = info.endPos;
        });

        FMSocketIOManager.instance.On("Turn_Result", (e) =>
         {
             UserTurn turn = JsonUtility.FromJson<UserTurn>(e.data);
             MyTurn = turn.userTurn;
             _notificationTimer += 0;
             InGameCanvas.transform.Find("TurnText").gameObject.SetActive(true);
         }); 
    }

    IEnumerator NotificationTimer()
    {
        while (true)
        {
            _notificationTimer += 1;
            if (_notificationTimer >= 90)
            {
                InGameCanvas.transform.Find("TurnText").gameObject.SetActive(false);
                _notificationTimer = 0;
            }
            yield return null;
        }
    }
}

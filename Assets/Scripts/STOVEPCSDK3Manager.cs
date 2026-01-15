using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PC SDK 3.0 紐⑤뱢 蹂?Using 援щЦ???꾩슂?⑸땲??
using static Stove.PCSDK.Base;
using static Stove.PCSDK.GameSupport;

public class STOVEPCSDK3Manager : MonoBehaviour
{
    // ?대옒???곷떒???꾩슂??蹂?섎? ?좎뼵?⑸땲??

    // 珥덇린???щ?瑜???ν븯湲??꾪븳 蹂??
    private bool _isInitialized;

    // 肄붾（???ㅽ뻾 二쇨린瑜???ν븯湲??꾪븳 蹂??
    private float _runCallbackInternval = 1.0f;

    // RunCallbackLoop 肄붾（?댁쓣 ??ν븯湲??꾪븳 蹂??
    private Coroutine _runCallbackCoroutine;

    // ?ㅻ툕?앺듃瑜?Singleton ?뺥깭濡??ъ슜?덇? ?꾪븳 ?뺤쟻 蹂??
    private static STOVEPCSDK3Manager _instance;
    private static object _lockObject = new object();

    public static STOVEPCSDK3Manager Instance
    {
        get
        {
            lock (_lockObject)
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<STOVEPCSDK3Manager>();

                    if (_instance == null)
                    {
                        _instance = new GameObject().AddComponent<STOVEPCSDK3Manager>();
                        _instance.name = "STOVEPCSDK3Manager";
                    }
                }
            }

            return _instance;
        }
    }

    #region Unity Methods

    // DontDestroyOnLoad 泥섎━瑜?吏꾪뻾
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    // OnDestroy ?먯꽌 UnInitialize ?몄텧
    private void OnDestroy()
    {
        if (_isInitialized)
        {
            UnInitialize();
        }
    }

    #endregion

    #region Coroutine

    // RunCallback??泥섎━?섍린 ?꾪븳 肄붾（?댁쓣 ?묒꽦
    private IEnumerator RunCallbackCoroutine()
    {
        var wfs = new WaitForSeconds(_runCallbackInternval);

        while (true)
        {
            Base_RunCallback();
            yield return wfs;
        }
    }

    #endregion

    #region STOVEPCSDK3Manager public methods

    // Result 援ъ“泥?異쒕젰 硫붿꽌??
    public void PrintResult(Result r)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("# Result");
        sb.AppendLine($" - Result.sdkName : {r.sdkName}");
        sb.AppendLine($" - Result.methodCode : {r.methodCode}");
        sb.AppendLine($" - Result.resultCode : {r.resultCode}");
        sb.AppendLine($" - Result.exceptionMessage : {r.exceptionMessage}");

        Debug.Log(sb.ToString());
    }

    // CallbackResult 援ъ“泥?異쒕젰 硫붿꽌??
    public void PrintCallbackResult(CallbackResult cr)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("# CallbackResult");
        sb.AppendLine($" - CallbackResult.Result.sdkName : {cr.result.sdkName}");
        sb.AppendLine($" - CallbackResult.Result.methodCode : {cr.result.methodCode}");
        sb.AppendLine($" - CallbackResult.Result.resultCode : {cr.result.resultCode}");
        sb.AppendLine($" - CallbackResult.Result.exceptionMessage : {cr.result.exceptionMessage}");
        sb.AppendLine($" - CallbackResult.externalError : {cr.externalError}");

        Debug.Log(sb.ToString());
    }

    // [?섏젙] 怨듭떇 臾몄꽌???덉감???곕씪 珥덇린??濡쒖쭅 蹂寃?
    public void Initialize()
    {
        StartRunCallbackLoop();

        StovePCInitializeParam initParam = new StovePCInitializeParam
        {
            environment = "LIVE",
            gameId = "GM-2617-6910DAF6_IND",
            applicationKey = "9d3c59efa9cc2681a7121713f2d54796974c362e91ce723ff4e4b457779d1ecb"
        };
        
        Debug.Log("Calling Base_RestartAppIfNecessaryAsync...");
        Base_RestartAppIfNecessaryAsync(initParam, 60000, (CallbackResult cbResult, bool restartNeeded) =>
        {
            Debug.Log("Base_RestartAppIfNecessaryAsync callback received.");
            PrintCallbackResult(cbResult);

            if (restartNeeded)
            {
                Debug.LogError("Execution via STOVE Launcher is required. Please exit the application.");
                // Application.Quit(); // ?ㅼ젣 鍮뚮뱶?먯꽌???곗쿂瑜??듯빐 ?ㅽ뻾?섏? ?딆븯?쇰?濡?醫낅즺?댁빞 ?⑸땲??
                return;
            }

            Debug.Log("Proceeding with Base_Initialize...");
            Base_Initialize(initParam, (CallbackResult initCbResult) =>
            {
                Debug.Log("Base_Initialize callback received.");
                PrintCallbackResult(initCbResult);

                if (initCbResult.result.IsSuccessful())
                {
                    Debug.Log("STOVE Base SDK initialized successfully.");
                    _isInitialized = true;

                    Result gsInitResult = GameSupport_Initialize();
                    PrintResult(gsInitResult);

                    if (gsInitResult.IsSuccessful())
                    {
                        Debug.Log("STOVE GameSupport SDK initialized successfully.");
                        // [?섏젙] SDK 珥덇린???깃났 ???좎? ?뺣낫 ?붿껌
                        RequestUserInfo();
                        UpdateGameStartAchievement();
                    }
                    else
                    {
                        Debug.LogError("Failed to initialize STOVE GameSupport SDK.");
                    }
                }
                else
                {
                    Debug.LogError("Failed to initialize STOVE Base SDK.");
                }
            });
        });
    }

    // [異붽?] ?좎? ?됰꽕?꾩쓣 ??ν븷 ?띿꽦
    public string UserNickname { get; private set; }

    // [異붽?] ?좎? ?뺣낫瑜??붿껌?섎뒗 硫붿꽌??
    public void RequestUserInfo()
    {
        if (!_isInitialized)
        {
            Debug.LogError("SDK not initialized. Cannot request user info.");
            return;
        }

        Debug.Log("Requesting user information from STOVE...");
        
        // 1. ?좎? ?뺣낫瑜?梨꾩슱 媛앹껜瑜??좎뼵?⑸땲?? ?ㅻ쪟 硫붿떆吏???섏삩 ?뺥솗??????대쫫???ъ슜?⑸땲??
        Stove.PCSDK.Base.StovePCUser user = new Stove.PCSDK.Base.StovePCUser();

        // 2. ?⑥닔瑜??몄텧?섍퀬, 寃곌낵瑜?諛쏆뒿?덈떎. user 媛앹껜瑜?ref ?ㅼ썙?쒕줈 ?섍꺼以띾땲??
        Result result = Base_GetUser(ref user);

        // 3. 諛섑솚??寃곌낵媛믪쓣 ?뺤씤?⑸땲??
        Debug.Log("====== Base_GetUser Result ======");
        PrintResult(result);
        if (result.IsSuccessful())
        {
            UserNickname = user.nickname;
            //Debug.Log($"?좎? ?뺣낫 ?띾뱷 ?깃났: Nickname = {UserNickname}, MemberNo = {user.memberNumber}, GameUserId = {user.gameUserId}");
        }
        else
        {
            Debug.LogError("?좎? ?뺣낫 ?띾뱷 ?ㅽ뙣.");
            UserNickname = "StoveUser"; // ?ㅽ뙣 ??湲곕낯媛??ㅼ젙
        }
    }


    public void UpdateGameStartAchievement()
    {
        string statId = "GAMESTART";
        int valueToIncrement = 1;

        Debug.Log($"Attempting to modify stat: {statId} with value: {valueToIncrement}");

        GameSupport_ModifyStat(statId, valueToIncrement, (CallbackResult cr, StovePCModifyStatValue modifiedStat) =>
        {
            Debug.Log("====== GameSupport_ModifyStat Callback ======");
            PrintCallbackResult(cr);
            if (cr.result.IsSuccessful())
            {
                Debug.Log($"?꾩쟾怨쇱젣 '{statId}' ?ㅽ꺈 ?낅뜲?댄듃 ?깃났!");
            }
            else
            {
                Debug.LogError($"?꾩쟾怨쇱젣 '{statId}' ?ㅽ꺈 ?낅뜲?댄듃 ?ㅽ뙣.");
            }
        });
    }

    // 紐⑤뱢 ?듯빀 ?뺣━瑜??꾪븳 UnInitialize 硫붿냼???묒꽦
    public void UnInitialize()
    {
        Result result;

        this.StopRunCallbackLoop();

        result = GameSupport_UnInitialize();
        PrintResult(result);

        result = Base_UnInitialize();
        PrintResult(result);
        
        _isInitialized = false;
        Debug.Log("All STOVE SDK modules uninitialized.");
    }

    // RunCallback??二쇨린?곸쑝濡??몄텧?섍린 ?꾪븳 硫붿냼???묒꽦
    public void StartRunCallbackLoop()
    {
        if (_runCallbackCoroutine == null)
        {
            Debug.Log("Start RunCallbackLoop");

            _runCallbackCoroutine = StartCoroutine(RunCallbackCoroutine());
        }
    }

    // Coroutine??以묒??섍린 ?꾪븳 硫붿냼???묒꽦
    public void StopRunCallbackLoop()
    {
        if (_runCallbackCoroutine != null)
        {
            Debug.Log("Stop RunCallbackLoop");

            StopCoroutine(_runCallbackCoroutine);
            _runCallbackCoroutine = null;
        }
    }

    #endregion
}

using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PC SDK 3.0 모듈 별 Using 구문이 필요합니다.
using static Stove.PCSDK.Base;
using static Stove.PCSDK.GameSupport;

public class STOVEPCSDK3Manager : MonoBehaviour
{
    // 클래스 상단에 필요한 변수를 선언합니다.

    // 초기화 여부를 저장하기 위한 변수
    private bool _isInitialized;

    // 코루틴 실행 주기를 저장하기 위한 변수
    private float _runCallbackInternval = 1.0f;

    // RunCallbackLoop 코루틴을 저장하기 위한 변수
    private Coroutine _runCallbackCoroutine;

    // 오브젝트를 Singleton 형태로 사용히가 위한 정적 변수
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
                    _instance = FindObjectOfType<STOVEPCSDK3Manager>();

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

    // DontDestroyOnLoad 처리를 진행
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    // OnDestroy 에서 UnInitialize 호출
    private void OnDestroy()
    {
        if (_isInitialized)
        {
            UnInitialize();
        }
    }

    #endregion

    #region Coroutine

    // RunCallback을 처리하기 위한 코루틴을 작성
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

    // Result 구조체 출력 메서드
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

    // CallbackResult 구조체 출력 메서드
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

    // [수정] 공식 문서의 절차에 따라 초기화 로직 변경
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
                // Application.Quit(); // 실제 빌드에서는 런처를 통해 실행되지 않았으므로 종료해야 합니다.
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
                Debug.Log($"도전과제 '{statId}' 스탯 업데이트 성공!");
            }
            else
            {
                Debug.LogError($"도전과제 '{statId}' 스탯 업데이트 실패.");
            }
        });
    }

    // 모듈 통합 정리를 위한 UnInitialize 메소드 작성
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

    // RunCallback을 주기적으로 호출하기 위한 메소드 작성
    public void StartRunCallbackLoop()
    {
        if (_runCallbackCoroutine == null)
        {
            Debug.Log("Start RunCallbackLoop");

            _runCallbackCoroutine = StartCoroutine(RunCallbackCoroutine());
        }
    }

    // Coroutine을 중지하기 위한 메소드 작성
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
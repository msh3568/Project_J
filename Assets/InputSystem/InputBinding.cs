using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static Assets.InputSystem.EUserAction;

namespace Assets.InputSystem
{
    public class InputBinding : MonoBehaviour
    {
        private const string RebindsKey = "PlayerInputRebinds";

        [Header("Player 연결")]
        [SerializeField] private Player player;

        private PlayerInputSet InputSet => player != null ? player.input : null;

        private InputActionRebindingExtensions.RebindingOperation currentRebind;

        private bool inputLock = false;

        public void ApplyGameInputLock(bool flag)
        {
            if (InputSet == null) return;

            inputLock = flag;

            if (inputLock) InputSet.Player.Disable();
            else InputSet.Player.Enable();
        }

        private void Start()
        {
            if (player == null)
            {
                player = GetComponent<Player>() ?? FindFirstObjectByType<Player>();
            }

            if (InputSet == null)
            {
                Debug.LogError("InputRebindingManager: Player 또는 Player.input 없음");
                return;
            }

            LoadSetting();
        }

        public void SaveSetting()
        {
            if (InputSet == null) { return; }

            string json = InputSet.asset.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(RebindsKey, json);
            PlayerPrefs.Save();
        }

        public void LoadSetting()
        {
            if (InputSet == null) { return; }

            string json = PlayerPrefs.GetString(RebindsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                InputSet.asset.LoadBindingOverridesFromJson(json);
            }
        }

        public void ResetSetting()
        {
            if(InputSet == null) { return; }
            InputSet.asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(RebindsKey);
        }

        private int FindBindingIndex(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];

                if (b.isComposite || b.isPartOfComposite)
                    continue;

                if (!string.IsNullOrEmpty(b.groups) && b.groups.Contains("Keyboard"))
                    return i;
            }

            // Keyboard 태그가 없으면 그냥 첫 번째 non-composite 사용
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (!b.isComposite && !b.isPartOfComposite)
                    return i;
            }

            Debug.LogError($"[{action.name}] 키보드용 바인딩 인덱스 찾기 실패");
            return -1;
        }

        private int FindCompositePartIndex(InputAction action, string partName)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isPartOfComposite && b.name == partName)
                    return i;
            }

            Debug.LogError($"Composite [{action.name}] 안에서 '{partName}' 파트 찾기 실패");
            return -1;
        }

        private (InputAction action, int bindingIndex) GetActionAndBinding(UserAction userAction)
        {
            if (InputSet == null)
                return (null, -1);

            switch (userAction)
            {
                case UserAction.MoveLeft:
                    {
                        var move = InputSet.Player.Movement;
                        int idx = FindCompositePartIndex(move, "left");
                        return (move, idx);
                    }

                case UserAction.MoveRight:
                    {
                        var move = InputSet.Player.Movement;
                        int idx = FindCompositePartIndex(move, "right");
                        return (move, idx);
                    }

                case UserAction.Attack:
                    {
                        var act = InputSet.Player.Attack;
                        int idx = FindBindingIndex(act);
                        return (act, idx);
                    }

                case UserAction.Jump:
                    {
                        var act = InputSet.Player.Jump;
                        int idx = FindBindingIndex(act);
                        return (act, idx);
                    }

                case UserAction.Dash:
                    {
                        var act = InputSet.Player.Dash;
                        int idx = FindBindingIndex(act);
                        return (act, idx);
                    }

                case UserAction.Baldo:
                    {
                        var act = InputSet.Player.Baldo;
                        int idx = FindBindingIndex(act);
                        return (act, idx);
                    }

                case UserAction.Pary:
                    {
                        var act = InputSet.Player.Pary;
                        int idx = FindBindingIndex(act);
                        return (act, idx);
                    }

                case UserAction.Checkpoint:
                    {
                        var act = InputSet.Player.Checkpoint;
                        int idx = FindBindingIndex(act);
                        return (act, idx);
                    }
            }

            return (null, -1);
        }

        public void StartSetting(UserAction userAction, Action<string> onComplete = null)
        {
            var (action, bindingIndex) = GetActionAndBinding(userAction);

            if (action == null || bindingIndex < 0)
            {
                Debug.LogError($"[{userAction}] 리바인딩 대상 찾기 실패");
                onComplete?.Invoke(null);
                return;
            }

            StartRebindInternal(action, bindingIndex, onComplete);
        }

        private void StartRebindInternal(InputAction action, int bindingIndex, Action<string> onComplete)
        {
            currentRebind?.Cancel();

            action.Disable();

            currentRebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    if (!inputLock) action.Enable();
                    op.Dispose();
                    currentRebind = null;

                    SaveSetting();

                    string display = action.GetBindingDisplayString(bindingIndex);
                    onComplete?.Invoke(display);
                })
                .OnCancel(op =>
                {
                    if (!inputLock) action.Enable();
                    op.Dispose();
                    currentRebind = null;

                    onComplete?.Invoke(null);
                });

            currentRebind.Start();
        }
        public void OnClickResetAllBindings()
        {
            ResetSetting();

            // 씬에 있는 모든 InputRebindButton 찾아서 텍스트 갱신
            var buttons = FindObjectsByType<InputRebindButton>(FindObjectsSortMode.None);
            foreach (var b in buttons)
            {
                b.RefreshLabel();
            }

            Debug.Log("키 바인딩을 기본값으로 리셋하고, 모든 버튼 라벨을 갱신했습니다.");
        }

        public void CancelRebind()
        {
            currentRebind?.Cancel();
        }

        public string GetBindingDisplayString(UserAction userAction)
        {
            var (action, bindingIndex) = GetActionAndBinding(userAction);

            if (action == null || bindingIndex < 0)
                return string.Empty;

            return action.GetBindingDisplayString(bindingIndex);
        }
    }
}
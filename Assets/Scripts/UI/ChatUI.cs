using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GNW2.UI
{
    public class ChatUI : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_InputField _input;
        public event Action<string> OnMesageSent;

        private void Awake()
        {
            _button.onClick.AddListener(() =>
            {
                OnMesageSent?.Invoke(_input.text);
                _button.transform.parent.gameObject.SetActive(false);
            });
        }

        private void Update()
        {
            if(UnityEngine.Input.GetKeyUp(KeyCode.Alpha1))
            {
                _button.transform.parent.gameObject.SetActive(!_button.transform.parent.gameObject.activeSelf);
                _input.text = "";
            }
        }
    }
}

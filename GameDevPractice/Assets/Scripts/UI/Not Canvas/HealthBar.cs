// using System;
// using UnityEngine;
// using UnityEngine.Serialization;
//
// public class HealthBar : MonoBehaviour
// {
//     [FormerlySerializedAs("healthSystem")] [SerializeField] private HealthSystem healthSystem;
//     [SerializeField] private Transform barTransform;
//     
//     private void Awake()
//     {
//         barTransform = Util.FindChild<Transform>(gameObject, "bar", true);
//     }
//
//     private void Start()
//     {
//         healthSystem.OnDamage += Health_OnChanged;
//         healthSystem.OnHeal += Health_OnChanged;
//         healthSystem.OnRevived += Health_OnChanged;
//         UpdateBar();
//     }
//
//     private void Health_OnChanged(object sender, System.EventArgs e)
//     {
//         UpdateBar();
//     }
//
//     private void UpdateBar()
//     {
//         float result = healthSystem.GetHealthRatio();
//         if (result is > 0 and < 1)
//         {
//             barTransform.localScale = new Vector3(result, 0.5f, 1);
//             Show();
//         }
//         else
//             Hide();
//     }
//
//     void Show() => gameObject.SetActive(true);
//     void Hide() => gameObject.SetActive(false);
// }

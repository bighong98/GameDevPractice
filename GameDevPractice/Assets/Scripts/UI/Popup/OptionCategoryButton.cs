using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class OptionCategoryButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    public Button Button => button;
    public TMP_Text Label => label;
}

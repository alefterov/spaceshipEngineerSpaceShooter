using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class RateGameButton : MonoBehaviour
{
    private Button button;

    private void Awake() => button = GetComponent<Button>();

    private void OnEnable() => button.onClick.AddListener(GameRating.OpenStorePage);

    private void OnDisable() => button.onClick.RemoveListener(GameRating.OpenStorePage);
}

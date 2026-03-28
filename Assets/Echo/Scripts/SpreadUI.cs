using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpreadUI : MonoBehaviour
{
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private Image spreadIndicator;
    [SerializeField] private RectTransform crosshair;
    [SerializeField] private TextMeshProUGUI spreadText;
    [SerializeField] private Color minSpreadColor = Color.green;
    [SerializeField] private Color maxSpreadColor = Color.red;
    
    private readonly float baseCrosshairSize = 20f;
    
    void Update()
    {
        if (weaponController == null) return;
        
        float spreadPercent = weaponController.GetSpreadPercentage();
        
        // Изменение размера прицела
        if (crosshair != null)
        {
            float spreadSize = baseCrosshairSize * (1 + spreadPercent * 3);
            crosshair.sizeDelta = new Vector2(spreadSize, spreadSize);
        }
        
        // Изменение цвета индикатора
        if (spreadIndicator != null)
        {
            spreadIndicator.color = Color.Lerp(minSpreadColor, maxSpreadColor, spreadPercent);
            spreadIndicator.fillAmount = spreadPercent;
        }
        
        // Текстовое отображение
        if (spreadText != null)
        {
            spreadText.text = $"Spread: {weaponController.GetCurrentSpread():F1}";
        }
    }
}
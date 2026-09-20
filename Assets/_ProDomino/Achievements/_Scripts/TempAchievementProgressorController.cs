using ProDomino.AnalyticsSystem;
using Timba.Patterns;
using UnityEngine;
using static HelperSharedLibrary.Enums;

public class TempAchievementProgressorController : MonoBehaviour
{
    AnalyticsManager analyticsManager;

    private void Start()
    {
        analyticsManager = ServiceLocator.Instance.GetService<AnalyticsManager>();
    }

    private void Update()
    {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKey(KeyCode.RightShift))
        {
            if (Input.GetKeyDown(KeyCode.Keypad1))
            {
                analyticsManager.SendAnalytic(AnalyticType.TotalCompetitiveWins);
            } 
            
            else if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                analyticsManager.SendAnalytic(AnalyticType.AddFriends, 1);
            } 
            
            else if (Input.GetKeyDown(KeyCode.Keypad3))
            {
                analyticsManager.SendAnalytic(AnalyticType.CompleteTutorial);
            }

            else if (Input.GetKeyDown(KeyCode.Keypad4))
            {
                analyticsManager.SendAnalytic(AnalyticType.ObtainCosmetic);
            } 
            
            else if (Input.GetKeyDown(KeyCode.Keypad5))
            {
                analyticsManager.SendAnalytic(AnalyticType.PlaceTilesOnBoard, 10);
            } 
        }
#endif
    }
}

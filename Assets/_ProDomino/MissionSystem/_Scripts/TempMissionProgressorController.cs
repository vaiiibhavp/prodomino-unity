using ProDomino.AnalyticsSystem;
using Timba.Patterns;
using UnityEngine;
using static HelperSharedLibrary.Enums;

public class TempMissionProgressorController : MonoBehaviour
{
    AnalyticsManager analyticsManager;

    private void Start()
    {
        analyticsManager = ServiceLocator.Instance.GetService<AnalyticsManager>();
    }

    private void Update()
    {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKey(KeyCode.LeftShift))
        {
            if (Input.GetKeyDown(KeyCode.Keypad1))
            {
                analyticsManager.SendAnalytic(AnalyticType.MissionsCompleted, 1);
            } 
            
            else if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                analyticsManager.SendAnalytic(AnalyticType.GamesPlayed, 1);
            } 
            
            else if (Input.GetKeyDown(KeyCode.Keypad3))
            {
                analyticsManager.SendAnalytic(AnalyticType.TotalWins, 1);
            }

            else if (Input.GetKeyDown(KeyCode.Keypad4))
            {
                analyticsManager.SendAnalytic(AnalyticType.TotalCompetitiveWins, 1);
            } 
            
            else if (Input.GetKeyDown(KeyCode.Keypad5))
            {
                analyticsManager.SendAnalytic(AnalyticType.ConcentrateMatchTiles, 1);
            } 
        }
#endif
    }
}

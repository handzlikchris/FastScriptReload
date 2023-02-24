using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiscordDukeIssue : DiscordDukeIssueBase
{
    public void UpdateNearTank(Tank nearTank = null)
    {
        if (nearTank) {
            SetNearTank(nearTank); 
        }
    }
    
    public void SetNearTank(Tank newNearTank) {
        if (NearTank) {
            return;
        }
        //Debug.LogWarning("SetNearTank");
        NearTank = newNearTank;
        // OnSetNearTank?.Invoke();
    }
}

public class DiscordDukeIssueBase: MonoBehaviour
{
    public Tank NearTank;
}


public class Tank: MonoBehaviour
{
    
}
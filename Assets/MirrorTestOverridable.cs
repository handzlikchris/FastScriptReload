 // using System.Collections;
 // using System.Linq;
 // using Mirror;
 // using UnityEngine;
 //
 // public class MirrorTestOverridable : NetworkBehaviour 
 // {
 //     public override void OnStartServer()
 //     {
 //         StartCoroutine(ToggleAuthority());
 //     }
 //
 //     IEnumerator ToggleAuthority()
 //     {
 //         while (true)
 //         {
 //             var firstConnection = NetworkServer.connections.FirstOrDefault().Value;
 //
 //             if (firstConnection != null)  
 //             {
 //                 if (firstConnection.owned.Contains(netIdentity))
 //                 {
 //                     netIdentity.RemoveClientAuthority();
 //                 }
 //                 else
 //                 {
 //                   netIdentity.AssignClientAuthority(firstConnection);    
 //                 }
 //             }
 //
 //             yield return new WaitForSeconds(1);
 //         }
 //     } 
 //
 //     public override void OnStartAuthority()
 //     {
 //         Debug.Log($"Authority Start - overriden");
 //     }
 // }

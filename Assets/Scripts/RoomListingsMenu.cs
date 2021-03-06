﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class RoomListingsMenu : MonoBehaviourPunCallbacks
{
    [SerializeField]
    private Transform _content;
    [SerializeField]
    private RoomListing _roomListing;
    
    private List<RoomListing> _listings = new List<RoomListing>();
    
    public override void OnRoomListUpdate(List<RoomInfo> roomList) 
    {
      Debug.Log("Room list updated");
      foreach (RoomInfo info in roomList)  
      {
          int index = _listings.FindIndex(x => x.RoomInfo.Name == info.Name); 
          if (info.RemovedFromList) 
          {
              Debug.Log("Room to be removed from List: " + info.Name);            
              if (index != -1) 
              {
                 Destroy(_listings[index].gameObject);
                 _listings.RemoveAt(index);
              } 
          }
          else 
          {
              Debug.Log("Room added: " + info.Name);
              if (index != -1) {
                  Debug.Log("Updating existing room");
                  _listings[index].SetRoomInfo(info);
              }
              else 
              {
                  Debug.Log("Adding new room!");
                  RoomListing listing = Instantiate(_roomListing, _content);
                      if (listing != null)
                          Debug.Log("Added new room!");
                          listing.SetRoomInfo(info);
                          _listings.Add(listing);
              }
          }
      }
      
    }
}
  
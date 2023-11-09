using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UI.Notify;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyServers : MonoBehaviour, INotifier
{
    [SerializeField] private TextMeshProUGUI _serversCount;
    [SerializeField] private TextMeshProUGUI _fetchingLobbyTxt;
    [SerializeField] private TMP_InputField _searchServers;
    [SerializeField] private TMP_Dropdown _filters;
    [SerializeField] private Button _refreshBtn;
    [SerializeField] private LobbyObject _lobbyObject;
    [SerializeField] private Transform _parent;

    private List<LobbyObject> _lobbyObjects;
    private List<Lobby> _lobbies;
    private QueryLobbiesOptions _lobbyQueries = new ();
    
    private bool _isRefreshingLobbies;

    private void OnEnable()
    {
        _searchServers.onValueChanged.AddListener(OnSearch);
        _filters.onValueChanged.AddListener(FilterLobbies);
    }

    private void OnDisable()
    {
        _searchServers.onValueChanged.RemoveListener(OnSearch);
        _filters.onValueChanged.RemoveListener(FilterLobbies);
    }
    
    private void OnSearch(string search)
    {
        if(_lobbyObjects == null) return;
        
        if (search.Length == 0)
        {
            foreach (var lobbyObject in _lobbyObjects)
            {
                lobbyObject.gameObject.SetActive(true);
            }
            
            return;
        }
        
        var searchedLobbies = SearchManager.Instance.Search(_lobbyObjects, search);
        for (int i = 0; i < searchedLobbies.Count; i++)
        {
            for (int j = 0; j < _lobbyObjects.Count; j++)
            {
                _lobbyObjects[j].gameObject.SetActive(searchedLobbies[i].name == _lobbyObjects[j].name);
            }
        }
    }

    private void FilterLobbies(int options)
    {
        switch (options)
        {
            case 0:
                _lobbyQueries = new QueryLobbiesOptions
                {
                    Order = new List<QueryOrder>
                    {
                        new(asc: false, QueryOrder.FieldOptions.Created)
                    }
                };
                break;
            case 1:
                _lobbyQueries = new QueryLobbiesOptions
                {
                    Filters = new List<QueryFilter>
                    {
                        new(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                    }
                };
                break;
            default:
                _lobbyQueries = new QueryLobbiesOptions();
                break;
        }
        
        RefreshLobbies();
    }

    private void Start() => 
        _refreshBtn.onClick.AddListener(RefreshLobbies);

    public async void RefreshLobbies()
    {
        if(_isRefreshingLobbies) return;
        _isRefreshingLobbies = true;

        _lobbies ??= new List<Lobby>();
        _lobbyObjects ??= new List<LobbyObject>();
        ClearLobbies();
        
        try
        {
            _fetchingLobbyTxt.gameObject.SetActive(true);
            _fetchingLobbyTxt.SetText("Fetching Lobbies...");
            
            var response = await Lobbies.Instance.QueryLobbiesAsync(_lobbyQueries);
            var allLobbies = response.Results;

            var lobbiesToDiscard = allLobbies.Where(lobby => lobby.Data["START_GAME"].Value != "0" &&
                                                             lobby.Data["DestroyLobbyAfterSession"].Value == "true").ToList();

            foreach (var lobby in allLobbies.Where(lobby => !lobbiesToDiscard.Contains(lobby)))
            {
                _lobbies.Add(lobby);
            }
            
            if (_lobbies.Count == 0)
                _fetchingLobbyTxt.SetText("No Lobbies Found!");
            else
                _fetchingLobbyTxt.gameObject.SetActive(false);
        }
        catch (LobbyServiceException e)
        {
            NotificationHelper.SendNotification(NotificationType.Error, e.Message, this, NotifyCallType.Open);
            _isRefreshingLobbies = false;
            Debug.Log(e);
            throw;
        }

        _serversCount.SetText($"{_lobbies.Count}");
        
        foreach (var lobby in _lobbies)
        {
            var lobbyObject = Instantiate(_lobbyObject, _parent);
            lobbyObject.name = lobby.Name;
            var lobbyObjectData = new LobbyObjectData
            {
                LobbyName = lobby.Name, 
                LobbyID = lobby.Id,
                PlayersCount = lobby.Players.Count,
                TotalPlayers = lobby.MaxPlayers,
                HasPassword = lobby.HasPassword
            };
            lobbyObject.SetObjectsData(lobbyObjectData);
            _lobbyObjects.Add(lobbyObject);
        }
        
        _isRefreshingLobbies = false;
    }

    private void ClearLobbies()
    {
        foreach (var lobby in _lobbyObjects)
        {
            Destroy(lobby.gameObject);
        }
        
        if(_lobbyObjects.Count > 0)
            _lobbyObjects.Clear();
    }

    public void Notify(string notifyData)
    {
        
    }
}

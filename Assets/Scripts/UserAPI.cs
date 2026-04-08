using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class UserAPI : MonoBehaviour
{
    public static string baseUrl = "http://192.168.1.35:8080";

    public static IEnumerator GetUsers(Action<User[]> onSuccess, Action<string> onError = null)
    {
        using UnityWebRequest req = UnityWebRequest.Get(baseUrl + "/users");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            try
            {
                User[] users = JsonHelper.FromJson<User>(req.downloadHandler.text);
                onSuccess?.Invoke(users);
            }
            catch (Exception e)
            {
                onError?.Invoke("Error parseando usuarios: " + e.Message);
            }
        }
        else
        {
            onError?.Invoke(req.error);
        }
    }

    public static IEnumerator CreateUser(string name, Action onSuccess = null, Action<string> onError = null)
    {
        string url = baseUrl + "/users?name=" + UnityWebRequest.EscapeURL(name);

        using UnityWebRequest req = UnityWebRequest.PostWwwForm(url, "");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke();
        else
            onError?.Invoke(req.error);
    }

    public static IEnumerator DeleteUser(string id, Action onSuccess = null, Action<string> onError = null)
    {
        using UnityWebRequest req = UnityWebRequest.Delete(baseUrl + "/users/" + id);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke();
        else
            onError?.Invoke(req.error);
    }
}
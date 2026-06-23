using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class UserAPI : MonoBehaviour
{
    public static string baseUrl = "https://moving.cs.upc.edu/sensory_room_api/api/";

    [Serializable]
    private class CreateUserRequest
    {
        public string nom;
        public string tipus_sala;
        public string postura_inicial;
        public bool independent;
        public bool entorn_adult;
        public bool menu_mans_actiu;
        public bool particules_mans_actives;
    }

    [Serializable]
    private class UpdateUserRequest
    {
        public string nom;
        public string tipus_sala;
        public string postura_inicial;
        public bool independent;
        public bool entorn_adult;
        public bool menu_mans_actiu;
        public bool particules_mans_actives;
        public bool actiu;
    }

    [Serializable]
    private class CreateSessionRequest
    {
        public int id_usuari;
        public string postura_inicial;
        public bool menu_mans_actiu;
        public bool particules_mans_actives;
    }

    [Serializable]
    private class CreateSessionResponse
    {
        public int id_sessio;
    }

    [Serializable]
    private class EndSessionRequest
    {
        public double durada_total_segons;
        public double durada_tutorial_segons;
        public double durada_preparacio_segons;
        public double durada_vr_segons;
        public bool ha_entrat_tutorial;
        public bool ha_entrat_preparacio;
        public bool ha_entrat_vr;
        public string postura_final;
        public string observacions;
    }

    [Serializable]
    private class UpdateSessionSettingsRequest
    {
        public string postura_actual;
        public bool menu_mans_actiu;
        public bool particules_mans_actives;
    }

    [Serializable]
    private class CreatePhaseRequest
    {
        public string fase;
    }

    [Serializable]
    private class CreatePhaseResponse
    {
        public int id_sessio_fase;
    }

    [Serializable]
    private class EndDurationRequest
    {
        public double durada_segons;
    }

    [Serializable]
    private class ElementPoseRequest
    {
        public double posicio_x;
        public double posicio_y;
        public double posicio_z;
        public double rotacio_y;
    }

    [Serializable]
    private class CreateSessionElementRequest
    {
        public string id_element;
        public bool seleccionat;
        public int numero_posicio;
    }

    [Serializable]
    private class CreateTutorialElementResponse
    {
        public int id_tutorial_element;
    }

    [Serializable]
    private class CreateVrElementResponse
    {
        public int id_vr_element;
    }

    [Serializable]
    public class LastSessionElement
    {
        public string id_element;
        public int numero_posicio;
        public bool seleccionat = true;
    }

    [Serializable]
    public class LastSessionResponse
    {
        public int id_sessio;
        public string postura_inicial;
        public string postura_final;
        public string postura_actual;
        public bool menu_mans_actiu = true;
        public bool particules_mans_actives = true;
        public LastSessionElement[] preparation_elements;
        public LastSessionElement[] vr_elements;
    }

    [Serializable]
    public class SessionSummary
    {
        public int id_sessio;
        public string postura_inicial;
        public string postura_final;
        public string postura_actual;
        public bool menu_mans_actiu = true;
        public bool particules_mans_actives = true;
        public double durada_total_segons;
        public double durada_tutorial_segons;
        public double durada_preparacio_segons;
        public double durada_vr_segons;
        public bool ha_entrat_tutorial;
        public bool ha_entrat_preparacio;
        public bool ha_entrat_vr;
        public string observacions;
        public string iniciada_a;
        public string finalitzada_a;
        public string creat_a;
        public string actualitzat_a;
    }

    [Serializable]
    public class SessionPhaseInfo
    {
        public int id_sessio_fase;
        public string fase;
        public double durada_segons;
        public string iniciada_a;
        public string finalitzada_a;
    }

    [Serializable]
    public class SessionElementInfo
    {
        public string id_element;
        public bool seleccionat = true;
        public int numero_posicio;
        public double durada_segons;
        public double posicio_x;
        public double posicio_y;
        public double posicio_z;
        public double rotacio_y;
    }

    private static string Url(string path)
    {
        return baseUrl.TrimEnd('/') + path;
    }

    private static string NormalizeRoomType(string tipusSala, bool entornAdult)
    {
        if (tipusSala == "blanca" || tipusSala == "adult" || tipusSala == "infantil")
            return tipusSala;

        return entornAdult ? "adult" : "infantil";
    }

    private static string NormalizePosture(string postura)
    {
        return postura == "SENTADO" ? "SENTADO" : "DE_PIE";
    }

    public static IEnumerator GetUsers(Action<User[]> onSuccess, Action<string> onError = null)
    {
        using UnityWebRequest req = UnityWebRequest.Get(Url("/users"));
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
                onError?.Invoke("Error parseando usuarios: " + e.Message + "\nRespuesta: " + req.downloadHandler.text);
            }
        }
        else
        {
            onError?.Invoke(BuildError(req));
        }
    }

    public static IEnumerator CreateUser(
        string nom,
        string tipusSala,
        string posturaInicial,
        bool entornAdult,
        bool independent,
        bool menuMansActiu,
        bool particulesMansActives,
        Action onSuccess = null,
        Action<string> onError = null)
    {
        CreateUserRequest data = new CreateUserRequest
        {
            nom = nom,
            tipus_sala = NormalizeRoomType(tipusSala, entornAdult),
            postura_inicial = NormalizePosture(posturaInicial),
            independent = independent,
            entorn_adult = entornAdult,
            menu_mans_actiu = menuMansActiu,
            particules_mans_actives = particulesMansActives
        };

        yield return SendJson(
            Url("/users"),
            "POST",
            JsonUtility.ToJson(data),
            onSuccess,
            onError
        );
    }

    public static IEnumerator UpdateUser(
        User user,
        Action onSuccess = null,
        Action<string> onError = null)
    {
        UpdateUserRequest data = new UpdateUserRequest
        {
            nom = user.nom,
            tipus_sala = NormalizeRoomType(user.tipus_sala, user.entorn_adult),
            postura_inicial = NormalizePosture(user.postura_inicial),
            independent = user.independent,
            entorn_adult = user.entorn_adult,
            menu_mans_actiu = user.menu_mans_actiu,
            particules_mans_actives = user.particules_mans_actives,
            actiu = user.actiu
        };

        yield return SendJson(
            Url("/users/" + user.id_usuari),
            "PATCH",
            JsonUtility.ToJson(data),
            onSuccess,
            onError
        );
    }

    public static IEnumerator DeleteUser(int userId, Action onSuccess = null, Action<string> onError = null)
    {
        using UnityWebRequest req = UnityWebRequest.Delete(Url("/users/" + userId));
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke();
        }
        else
        {
            onError?.Invoke(BuildError(req));
        }
    }

    public static IEnumerator CreateSession(
        int userId,
        string posturaInicial,
        bool menuMansActiu,
        bool particulesMansActives,
        Action<int> onSuccess,
        Action<string> onError = null)
    {
        CreateSessionRequest data = new CreateSessionRequest
        {
            id_usuari = userId,
            postura_inicial = posturaInicial,
            menu_mans_actiu = menuMansActiu,
            particules_mans_actives = particulesMansActives
        };

        yield return SendJsonWithResponse<CreateSessionResponse>(
            Url("/sessions"),
            "POST",
            JsonUtility.ToJson(data),
            response => onSuccess?.Invoke(response.id_sessio),
            onError
        );
    }

    public static IEnumerator GetLastSession(
        int userId,
        Action<LastSessionResponse> onSuccess,
        Action<string> onError = null)
    {
        using UnityWebRequest req = UnityWebRequest.Get(Url("/users/" + userId + "/last-session"));
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            try
            {
                LastSessionResponse session = JsonUtility.FromJson<LastSessionResponse>(req.downloadHandler.text);
                onSuccess?.Invoke(session);
            }
            catch (Exception e)
            {
                onError?.Invoke("Error parseando ultima sesion: " + e.Message + "\nRespuesta: " + req.downloadHandler.text);
            }
        }
        else if (req.responseCode == 404)
        {
            onSuccess?.Invoke(null);
        }
        else
        {
            onError?.Invoke(BuildError(req));
        }
    }

    public static IEnumerator GetUserSessions(int userId, Action<SessionSummary[]> onSuccess, Action<string> onError = null)
    {
        if (userId <= 0)
        {
            onError?.Invoke("Id de usuario invalido: " + userId);
            yield break;
        }

        yield return GetJsonArray(
            Url("/users/" + userId + "/sessions"),
            onSuccess,
            onError
        );
    }

    public static IEnumerator GetSessionPhases(int sessionId, Action<SessionPhaseInfo[]> onSuccess, Action<string> onError = null)
    {
        yield return GetJsonArray(
            Url("/sessions/" + sessionId + "/phases"),
            onSuccess,
            onError
        );
    }

    public static IEnumerator GetSessionVrElements(int sessionId, Action<SessionElementInfo[]> onSuccess, Action<string> onError = null)
    {
        yield return GetJsonArray(
            Url("/sessions/" + sessionId + "/vr-elements"),
            onSuccess,
            onError
        );
    }

    public static IEnumerator GetSessionPreparationElements(int sessionId, Action<SessionElementInfo[]> onSuccess, Action<string> onError = null)
    {
        yield return GetJsonArray(
            Url("/sessions/" + sessionId + "/preparation-elements"),
            onSuccess,
            onError
        );
    }

    public static IEnumerator GetSessionTutorialElements(int sessionId, Action<SessionElementInfo[]> onSuccess, Action<string> onError = null)
    {
        yield return GetJsonArray(
            Url("/sessions/" + sessionId + "/tutorial-elements"),
            onSuccess,
            onError
        );
    }

    public static IEnumerator EndSession(
        int sessionId,
        double duradaTotalSegons,
        double duradaTutorialSegons,
        double duradaPreparacioSegons,
        double duradaVrSegons,
        bool haEntratTutorial,
        bool haEntratPreparacio,
        bool haEntratVr,
        string posturaFinal,
        string observacions,
        Action<string> onError = null)
    {
        EndSessionRequest data = new EndSessionRequest
        {
            durada_total_segons = duradaTotalSegons,
            durada_tutorial_segons = duradaTutorialSegons,
            durada_preparacio_segons = duradaPreparacioSegons,
            durada_vr_segons = duradaVrSegons,
            ha_entrat_tutorial = haEntratTutorial,
            ha_entrat_preparacio = haEntratPreparacio,
            ha_entrat_vr = haEntratVr,
            postura_final = posturaFinal,
            observacions = observacions
        };

        yield return SendJson(
            Url("/sessions/" + sessionId + "/end"),
            "PATCH",
            JsonUtility.ToJson(data),
            null,
            onError
        );
    }

    public static IEnumerator UpdateSessionSettings(
        int sessionId,
        string posturaActual,
        bool menuMansActiu,
        bool particulesMansActives,
        Action<string> onError = null)
    {
        UpdateSessionSettingsRequest data = new UpdateSessionSettingsRequest
        {
            postura_actual = posturaActual,
            menu_mans_actiu = menuMansActiu,
            particules_mans_actives = particulesMansActives
        };

        yield return SendJson(
            Url("/sessions/" + sessionId + "/settings"),
            "PATCH",
            JsonUtility.ToJson(data),
            null,
            onError
        );
    }

    public static IEnumerator CreateSessionPhase(
        int sessionId,
        string fase,
        Action<int> onSuccess,
        Action<string> onError = null)
    {
        CreatePhaseRequest data = new CreatePhaseRequest { fase = fase };

        yield return SendJsonWithResponse<CreatePhaseResponse>(
            Url("/sessions/" + sessionId + "/phases"),
            "POST",
            JsonUtility.ToJson(data),
            response => onSuccess?.Invoke(response.id_sessio_fase),
            onError
        );
    }

    public static IEnumerator EndSessionPhase(int sessionPhaseId, double duradaSegons, Action<string> onError = null)
    {
        EndDurationRequest data = new EndDurationRequest { durada_segons = duradaSegons };

        yield return SendJson(
            Url("/session-phases/" + sessionPhaseId + "/end"),
            "PATCH",
            JsonUtility.ToJson(data),
            null,
            onError
        );
    }

    public static IEnumerator CreateTutorialElement(
        int sessionId,
        string elementId,
        Action<int> onSuccess,
        Action<string> onError = null)
    {
        CreateSessionElementRequest data = new CreateSessionElementRequest
        {
            id_element = elementId,
            seleccionat = true
        };

        yield return SendJsonWithResponse<CreateTutorialElementResponse>(
            Url("/sessions/" + sessionId + "/tutorial-elements"),
            "POST",
            JsonUtility.ToJson(data),
            response => onSuccess?.Invoke(response.id_tutorial_element),
            onError
        );
    }

    public static IEnumerator EndTutorialElement(int tutorialElementId, double duradaSegons, Action<string> onError = null)
    {
        EndDurationRequest data = new EndDurationRequest { durada_segons = duradaSegons };

        yield return SendJson(
            Url("/tutorial-elements/" + tutorialElementId + "/end"),
            "PATCH",
            JsonUtility.ToJson(data),
            null,
            onError
        );
    }

    public static IEnumerator UpdateTutorialElementPose(
        int tutorialElementId,
        double posicioX,
        double posicioY,
        double posicioZ,
        double rotacioY,
        Action<string> onError = null)
    {
        ElementPoseRequest data = new ElementPoseRequest
        {
            posicio_x = posicioX,
            posicio_y = posicioY,
            posicio_z = posicioZ,
            rotacio_y = rotacioY
        };

        yield return SendJson(
            Url("/tutorial-elements/" + tutorialElementId + "/pose"),
            "PATCH",
            JsonUtility.ToJson(data),
            null,
            onError
        );
    }

    public static IEnumerator CreatePreparationElement(
        int sessionId,
        string elementId,
        bool seleccionat,
        Action<string> onError = null)
    {
        CreateSessionElementRequest data = new CreateSessionElementRequest
        {
            id_element = elementId,
            seleccionat = seleccionat
        };

        yield return SendJson(
            Url("/sessions/" + sessionId + "/preparation-elements"),
            "POST",
            JsonUtility.ToJson(data),
            null,
            onError
        );
    }

    public static IEnumerator CreateVrElement(
        int sessionId,
        string elementId,
        int numeroPosicion,
        Action<int> onSuccess,
        Action<string> onError = null)
    {
        CreateSessionElementRequest data = new CreateSessionElementRequest
        {
            id_element = elementId,
            seleccionat = true,
            numero_posicio = numeroPosicion
        };

        yield return SendJsonWithResponse<CreateVrElementResponse>(
            Url("/sessions/" + sessionId + "/vr-elements"),
            "POST",
            JsonUtility.ToJson(data),
            response => onSuccess?.Invoke(response.id_vr_element),
            onError
        );
    }

    public static IEnumerator EndVrElement(int vrElementId, double duradaSegons, Action<string> onError = null)
    {
        EndDurationRequest data = new EndDurationRequest { durada_segons = duradaSegons };

        yield return SendJson(
            Url("/vr-elements/" + vrElementId + "/end"),
            "PATCH",
            JsonUtility.ToJson(data),
            null,
            onError
        );
    }

    public static IEnumerator UpdateVrElementPose(
        int vrElementId,
        double posicioX,
        double posicioY,
        double posicioZ,
        double rotacioY,
        Action<string> onError = null)
    {
        ElementPoseRequest data = new ElementPoseRequest
        {
            posicio_x = posicioX,
            posicio_y = posicioY,
            posicio_z = posicioZ,
            rotacio_y = rotacioY
        };

        yield return SendJson(
            Url("/vr-elements/" + vrElementId + "/pose"),
            "PATCH",
            JsonUtility.ToJson(data),
            null,
            onError
        );
    }

    private static IEnumerator SendJson(
        string url,
        string method,
        string json,
        Action onSuccess,
        Action<string> onError)
    {
        using UnityWebRequest req = new UnityWebRequest(url, method);

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke();
        }
        else
        {
            onError?.Invoke(BuildError(req));
        }
    }

    private static IEnumerator SendJsonWithResponse<T>(
        string url,
        string method,
        string json,
        Action<T> onSuccess,
        Action<string> onError)
    {
        using UnityWebRequest req = new UnityWebRequest(url, method);

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            try
            {
                T response = JsonUtility.FromJson<T>(req.downloadHandler.text);
                onSuccess?.Invoke(response);
            }
            catch (Exception e)
            {
                onError?.Invoke("Error parseando respuesta: " + e.Message + "\nRespuesta: " + req.downloadHandler.text);
            }
        }
        else
        {
            onError?.Invoke(BuildError(req));
        }
    }

    private static IEnumerator GetJsonArray<T>(string url, Action<T[]> onSuccess, Action<string> onError)
    {
        Debug.Log("[API] GET " + url);

        using UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(BuildError(req));
            yield break;
        }

        try
        {
            onSuccess?.Invoke(JsonHelper.FromJson<T>(req.downloadHandler.text));
        }
        catch (Exception e)
        {
            onError?.Invoke("Error parseando respuesta: " + e.Message + "\nRespuesta: " + req.downloadHandler.text);
        }
    }

    private static string BuildError(UnityWebRequest req)
    {
        string errorMsg = req.error;

        if (!string.IsNullOrEmpty(req.downloadHandler.text))
            errorMsg += "\n" + req.downloadHandler.text;

        return errorMsg;
    }
}

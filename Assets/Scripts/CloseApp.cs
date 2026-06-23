using System.Collections;
using UnityEngine;

public class CloseApp : MonoBehaviour
{
    private bool closing;

    // Update is called once per frame
    public void Close()
    {
        if (closing)
            return;

        StartCoroutine(CloseBothApplications());
    }

    private IEnumerator CloseBothApplications()
    {
        closing = true;

        SessionTracker sessionTracker = SessionTracker.Instance;
        if (sessionTracker != null)
            yield return StartCoroutine(sessionTracker.EndSessionAndWait("Aplicacion cerrada desde la tablet."));

        // La tablet es la fuente de persistencia en modo multidispositivo.
        // Solo pedimos cerrar el visor cuando el cierre de sesion ya ha acabado.
        Connection connection = FindFirstObjectByType<Connection>();
        if (connection != null && connection.connected)
        {
            connection.Send("CMD_APP_QUIT");
            yield return new WaitForSecondsRealtime(0.5f);
        }

        Application.Quit();
    }
}

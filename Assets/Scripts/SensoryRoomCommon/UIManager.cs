using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject connectionCanvas;
    public GameObject mainCanvas;
    //public GameObject lightCanvas;
    //public GameObject soundCanvas;
    //public GameObject environmentCanvas;
    public GameObject userSelectCanvas;

    private void Start()
    {
        if (userSelectCanvas != null)
            GoToUserSelectCanvas();
        else if (connectionCanvas != null)
            GoToConnectionCanvas();
        else
            GoToMainCanvas();
    }

	public void ShowCanvas(GameObject canvas) 
    {
        mainCanvas.SetActive(false);
		//lightCanvas.SetActive(false);
		//soundCanvas.SetActive(false);
		//environmentCanvas.SetActive(false);
        userSelectCanvas.SetActive(false);
        connectionCanvas.SetActive(false);
    
		canvas.SetActive(true);
    }

    public void GoToConnectionCanvas() => ShowCanvas(connectionCanvas);
    public void GoToUserSelectCanvas() => ShowCanvas(userSelectCanvas);
    public void GoToMainCanvas() => ShowCanvas(mainCanvas);
  //  public void GoToLightCanvas() => ShowCanvas(lightCanvas);
 //   public void GoToSoundCanvas() => ShowCanvas(soundCanvas);
  //  public void GoToEnvironmentCanvas() => ShowCanvas(environmentCanvas);
}

using UnityEngine;
using UnityEngine.UI;
using System;

/* Class used specifically in the SensoryRoom application to encode a message
 
 Represents things like:
 a) "set button 2's property "clicked" to true"
 b) "set dropdown 0's current value to 1"
 */
[Serializable]
public class UpdateMessage
{
	// What type of UI component? [button/dropdown/etc]
	public string type;

	// Which specific element in the scene of that type? [0/1/2/...]
	public string element;

	// What property of that UI element is to be modified? [clicked/value/selected/etc]
	public string property;

	// To what value is the value of the UI element being updated to? [true/false/2/3/"hello"/etc]
	public string value;
}

public class UIState : MonoBehaviour
{
	public Connection connection;

	[Header("Sound")]
	public Button[] soundButtons;
	public Button[] soundVolumeButtons;
	public Sprite volumeActiveSprite;
	public Sprite volumeInactiveSprite;

	public Sprite activeSprite;
	public Sprite inactiveSprite;

	private bool[] activeSoundButtons;
	private int currentVolumeButton;

	[Header("Light")]
	public Button[] lightButtons;
	private bool[] activeLightButtons;
	public Button[] lightIntensityButtons;

	private int currentIntensityButton;

	[Header("Environment")]
	public Button[] environmentButtons;
	private bool[] activeEnvironmentButtons;

	public static bool applyingFromNetwork = false;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		activeSoundButtons = new bool[soundButtons.Length];
		activeLightButtons = new bool[lightButtons.Length];
		activeEnvironmentButtons = new bool[environmentButtons.Length];

		currentVolumeButton = soundVolumeButtons.Length / 2;
		currentIntensityButton = lightIntensityButtons.Length / 2;

		for (int i = 0; i < soundButtons.Length; ++i) 
		{
			// To capture the specific value of i instead of the i variable
			int button_index = i;
			soundButtons[i].onClick.AddListener(() =>
			{
				if (applyingFromNetwork)
					return;
				SendUpdate("sound", button_index.ToString(), "click", "true");
			});

			soundButtons[i].onClick.AddListener(() => 
			{
				// Disable all other buttons
				for (int j = 0; j < soundButtons.Length; ++j) 
				{
					if (j != button_index && activeSoundButtons[j]) 
					{
						activeSoundButtons[j] = false;
						soundButtons[j].GetComponent<Image>().sprite = inactiveSprite;
					}
				}

				// Toggle this button
				activeSoundButtons[button_index] = !activeSoundButtons[button_index];
				if (activeSoundButtons[button_index])
					soundButtons[button_index].GetComponent<Image>().sprite = activeSprite;
				else
					soundButtons[button_index].GetComponent<Image>().sprite = inactiveSprite;
			});
		}

		for (int i = 0; i < soundVolumeButtons.Length; ++i)
		{
			// To capture the specific value of i instead of the i variable
			int button_index = i;
			soundVolumeButtons[i].onClick.AddListener(() =>
			{
				if (applyingFromNetwork)
					return;
				SendUpdate("volume", button_index.ToString(), "click", "true");
			});

			soundVolumeButtons[i].onClick.AddListener(() =>
			{
				currentVolumeButton = button_index;

				// Button 0 is the 'mute' button, with a different image that never changes
				for (int j = 1; j < soundVolumeButtons.Length; ++j) 
				{
					if (j <= button_index)
						soundVolumeButtons[j].gameObject.GetComponent<Image>().sprite = volumeActiveSprite; 
					else 
						soundVolumeButtons[j].gameObject.GetComponent<Image>().sprite = volumeInactiveSprite;
				}
			});
		}

		for (int i = 0; i < lightButtons.Length; ++i)
		{
			// To capture the specific value of i instead of the i variable
			int button_index = i;
			lightButtons[i].onClick.AddListener(() =>
			{
				if (applyingFromNetwork)
					return;
				SendUpdate("light", button_index.ToString(), "click", "true");
			});

			lightButtons[i].onClick.AddListener(() =>
			{
				// Disable all other buttons
				for (int j = 0; j < lightButtons.Length; ++j)
				{
					if (j != button_index && activeLightButtons[j])
					{
						activeLightButtons[j] = false;
						lightButtons[j].GetComponent<Image>().sprite = inactiveSprite;
					}
				}

				// Toggle this button
				activeLightButtons[button_index] = !activeLightButtons[button_index];
				if (activeLightButtons[button_index])
					lightButtons[button_index].GetComponent<Image>().sprite = activeSprite;
				else
					lightButtons[button_index].GetComponent<Image>().sprite = inactiveSprite;
			});
		}

		for (int i = 0; i < lightIntensityButtons.Length; ++i)
		{
			// To capture the specific value of i instead of the i variable
			int button_index = i;
			lightIntensityButtons[i].onClick.AddListener(() =>
			{
				if (applyingFromNetwork)
					return;
				SendUpdate("intensity", button_index.ToString(), "click", "true");
			});

			lightIntensityButtons[i].onClick.AddListener(() =>
			{
				currentIntensityButton = button_index;

				// Button 0 is the 'no light' button, with a different image that never changes
				for (int j = 1; j < lightIntensityButtons.Length; ++j)
				{
					if (j <= button_index)
						lightIntensityButtons[j].gameObject.GetComponent<Image>().sprite = volumeActiveSprite;
					else
						lightIntensityButtons[j].gameObject.GetComponent<Image>().sprite = volumeInactiveSprite;
				}
			});
		}

		for (int i = 0; i < environmentButtons.Length; ++i)
		{
			// To capture the specific value of i instead of the i variable
			int button_index = i;
			environmentButtons[i].onClick.AddListener(() =>
			{
				if (applyingFromNetwork)
					return;
				SendUpdate("environment", button_index.ToString(), "click", "true");
			});

			environmentButtons[i].onClick.AddListener(() =>
			{
				// Disable all other buttons
				for (int j = 0; j < environmentButtons.Length; ++j)
				{
					if (j != button_index && activeEnvironmentButtons[j])
					{
						activeEnvironmentButtons[j] = false;
						environmentButtons[j].GetComponent<Image>().sprite = inactiveSprite;
					}
				}

				// Toggle this button
				activeEnvironmentButtons[button_index] = !activeEnvironmentButtons[button_index];
				if (activeEnvironmentButtons[button_index])
					environmentButtons[button_index].GetComponent<Image>().sprite = activeSprite;
				else
					environmentButtons[button_index].GetComponent<Image>().sprite = inactiveSprite;
			});
		}

		if (connection.side == Connection.Side.Server) 
		{
			lightButtons[0].onClick.Invoke();
			soundVolumeButtons[soundVolumeButtons.Length / 2].onClick.Invoke();
			lightIntensityButtons[lightIntensityButtons.Length / 2].onClick.Invoke();
		}

		connection.RegisterOnClientConnectCallback(() => SendCurrentState());
	}

	// Function to just encapsulate converting to a class
	void SendUpdate(string type, string element, string property, string value)
	{
		UpdateMessage msg = new();
		msg.type = type;
		msg.element = element;
		msg.property = property;
		msg.value = value;
		connection.Send(JsonUtility.ToJson(msg));
	}

	void SendCurrentState() 
	{
		for (int i = 0; i < activeSoundButtons.Length; ++i)
		{
			if (activeSoundButtons[i])
			{
				SendUpdate("sound", i.ToString(), "click", "true");
			}
		}

		SendUpdate("volume", currentVolumeButton.ToString(), "click", "true");

		for (int i = 0; i < activeLightButtons.Length; ++i)
		{
			if (activeLightButtons[i])
			{
				SendUpdate("light", i.ToString(), "click", "true");
			}
		}

		SendUpdate("intensity", currentIntensityButton.ToString(), "click", "true");

		for (int i = 0; i < activeEnvironmentButtons.Length; ++i)
		{
			if (activeEnvironmentButtons[i])
			{
				SendUpdate("environment", i.ToString(), "click", "true");
			}
		}
	}

	void Update()
    {
		if (connection.messageQueue.TryDequeue(out var update))
		{
			applyingFromNetwork = true;
			UpdateMessage updateMessage = JsonUtility.FromJson<UpdateMessage>(update);

			// Process update
			switch (updateMessage.type) 
			{
				case "sound":
					ManageSoundButton(updateMessage);
					break;
				case "volume":
					ManageVolumeButton(updateMessage);
					break;
				case "light":
					ManageLightButton(updateMessage);
					break;
				case "intensity":
					ManageLightIntensityButton(updateMessage);
					break;
				case "environment":
					ManageEnvironmentButton(updateMessage);
					break;
				default:
					Debug.LogWarning("UIState: Unrecognized option. Received a message with an unknown UI element type.");
					break;
			}

			applyingFromNetwork = false;
		}
	}

	void ManageSoundButton(UpdateMessage msg) 
	{
		int element = int.Parse(msg.element);
		soundButtons[element].onClick.Invoke();
	}

	void ManageVolumeButton(UpdateMessage msg)
	{
		int element = int.Parse(msg.element);
		soundVolumeButtons[element].onClick.Invoke();
	}

	void ManageLightButton(UpdateMessage msg) 
	{
		int element = int.Parse(msg.element);
		lightButtons[element].onClick.Invoke();
	}

	void ManageLightIntensityButton(UpdateMessage msg)
	{
		int element = int.Parse(msg.element);
		lightIntensityButtons[element].onClick.Invoke();
	}

	void ManageEnvironmentButton(UpdateMessage msg) 
	{
		int element = int.Parse(msg.element);
		environmentButtons[element].onClick.Invoke();
	}
}

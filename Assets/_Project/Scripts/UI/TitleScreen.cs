using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Margin.UI
{
    /// <summary>
    /// The title screen (spec 14): a full notebook page with the game's name and Play, Controls and Quit, in the same
    /// hand-drawn card and flat buttons as the pause menu. Keyboard, gamepad ("Menu" map) and mouse all work.
    /// Play opens the 3 save slots (SlotPanel); a slot continues where it was saved, or starts at UISettings.firstScene. Built by Margin > Build Title Screen into Scenes/Title.unity (first in
    /// Build Settings, so the built game opens here); the pause menu's Title Screen button comes back.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] private UISettings settings;
        [Tooltip("The MarginControls asset (its \"Menu\" map drives the buttons).")]
        [SerializeField] private InputActionAsset controls;

        private PanelSettings panel;
        private MenuView menu;
        private InputAction navigate, submit, cancel;

        public void Configure(UISettings uiSettings, InputActionAsset inputActions)
        {
            settings = uiSettings;
            controls = inputActions;
        }

        private void Awake()
        {
            Time.timeScale = 1f;   // in case we came here from the pause menu
            if (settings == null) settings = MarginUI.FindSettings();
            UIDocument document = MarginUI.CreateDocument(transform, settings, out panel);
            menu = new MenuView(document.rootVisualElement, settings, "MARGIN", new[]
            {
                MenuView.Panel("Play", settings.iconPlay, card => new SlotPanel(settings, card, Play)),
                MenuView.Options(settings),
                MenuView.Controls(settings),
                MenuView.Item("Quit", settings.iconQuit, MarginUI.QuitGame),
            }, fullPage: true, titleSize: settings.menuTitleSize * 2);
            menu.Open(controls);

            InputActionMap map = controls != null ? controls.FindActionMap("Menu") : null;
            if (map == null)
            {
                Debug.LogWarning("TitleScreen: no controls asset with a \"Menu\" map, so only the mouse works here.", this);
                return;
            }
            navigate = map.FindAction("Navigate");
            submit = map.FindAction("Submit");
            cancel = map.FindAction("Cancel");
            map.Enable();
        }

        private void OnDestroy()
        {
            if (panel != null) Destroy(panel);
        }

        private void Update()
        {
            if (menu == null) return;
            // Cancel on the main page does nothing here (there's nothing to go back to).
            menu.Update(navigate != null ? navigate.ReadValue<Vector2>() : Vector2.zero,
                        submit != null && submit.WasPressedThisFrame(),
                        cancel != null && cancel.WasPressedThisFrame(),
                        Time.unscaledDeltaTime * 60f);
        }

        /// <summary>Continue the slot (or start a new game in it) and load where it was saved.</summary>
        private void Play(int slot)
        {
            Margin.Save.GameSession.Begin(slot, settings.firstScene);
            string scene = Margin.Save.GameSession.Data.scene;
            if (!Application.CanStreamedLevelBeLoaded(scene)) scene = settings.firstScene;
            MarginUI.LoadScene(scene);
        }
    }
}

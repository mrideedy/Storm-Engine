using StormEngine.Core;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Language picker. Each language is shown in its own script; choosing one reloads the game in it.</summary>
    public sealed class LanguagePopup : Popup
    {
        public LanguagePopup(StormApp app) : base(L.T("LANGUAGE"), L.T("Pick the language for the whole game"), "globe")
        {
            var grid = Ui.Div("se-langgrid");
            foreach (var lang in Languages.All)
            {
                string code = lang.Code;
                bool current = code == Languages.Current.Code;
                var button = Ui.Btn("", null, current ? "green" : "white", "se-langbtn");
                button.clicked += () =>
                {
                    if (current)
                    {
                        Close();
                        return;
                    }
                    app.Sfx?.Click();
                    app.SetLanguage(code);
                };
                button.Add(Ui.Text(lang.Native, "se-lang-native"));
                if (lang.English != lang.Native) button.Add(Ui.Text(lang.English, "se-lang-english"));
                grid.Add(button);
            }
            BodyEl.Add(grid);
            Action(L.T("Close"), null, "white");
        }
    }
}

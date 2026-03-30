using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace XPXLevels;

public sealed class XPXNumberMenu : BaseMenu
{
    private readonly BasePlugin _plugin;

    public int ItemsPerPage { get; set; } = 6;
    public List<string> BodyLines { get; } = [];
    public string TitleColor { get; set; } = "gold";
    public string BodyColor { get; set; } = "silver";
    public string EnabledColor { get; set; } = "white";
    public string DisabledColor { get; set; } = "gray";
    public string PrevPageColor { get; set; } = "deepskyblue";
    public string NextPageColor { get; set; } = "deepskyblue";
    public string CloseColor { get; set; } = "tomato";

    public XPXNumberMenu(string title, BasePlugin plugin) : base(title)
    {
        _plugin = plugin;
    }

    public override void Open(CCSPlayerController player)
    {
        if (MenuManager.GetActiveMenu(player) is XPXNumberMenuInstance XPXMenu)
        {
            XPXMenu.Close();
        }
        else
        {
            MenuManager.CloseActiveMenu(player);
        }

        MenuManager.GetActiveMenus()[player.Handle] = new XPXNumberMenuInstance(_plugin, player, this);
        MenuManager.GetActiveMenus()[player.Handle].Display();
    }
}

public sealed class XPXNumberMenuInstance : BaseMenuInstance
{
    private readonly BasePlugin _plugin;

    public override int NumPerPage => Menu is XPXNumberMenu xpxMenu ? xpxMenu.ItemsPerPage : 6;

    protected override int MenuItemsPerPage => NumPerPage;

    public XPXNumberMenuInstance(BasePlugin plugin, CCSPlayerController player, IMenu menu) : base(player, menu)
    {
        _plugin = plugin;
        RemoveOnTickListener();
        plugin.RegisterListener<CounterStrikeSharp.API.Core.Listeners.OnTick>(Display);
    }

    public override void Display()
    {
        if (MenuManager.GetActiveMenu(Player) != this)
        {
            RemoveOnTickListener();
            return;
        }

        if (Menu is XPXNumberMenu menu)
        {
            var builder = new StringBuilder();
            builder.Append($"<b><font color='{menu.TitleColor}'>{menu.Title}</font></b>");
            builder.AppendLine("<br>");

            if (menu.BodyLines.Count > 0)
            {
                foreach (var bodyLine in menu.BodyLines)
                {
                    builder.Append($"<font color='{menu.BodyColor}'>{bodyLine}</font>");
                    builder.AppendLine("<br>");
                }

                if (menu.MenuOptions.Count > 0)
                {
                    builder.AppendLine("<br>");
                }
            }

            var keyOffset = 1;
            for (var index = CurrentOffset; index < Math.Min(CurrentOffset + MenuItemsPerPage, menu.MenuOptions.Count); index++)
            {
                var option = menu.MenuOptions[index];
                var color = option.Disabled ? menu.DisabledColor : menu.EnabledColor;
                builder.Append($"<font color='{color}'>{keyOffset++}.</font> {option.Text}");
                builder.AppendLine("<br>");
            }

            if (HasPrevButton)
            {
                builder.Append($"<font color='{menu.PrevPageColor}'>7.</font> {Application.Localizer["menu.button.previous"]}");
                builder.AppendLine("<br>");
            }

            if (HasNextButton)
            {
                builder.Append($"<font color='{menu.NextPageColor}'>8.</font> {Application.Localizer["menu.button.next"]}");
                builder.AppendLine("<br>");
            }

            if (menu.ExitButton)
            {
                builder.Append($"<font color='{menu.CloseColor}'>9.</font> {Application.Localizer["menu.button.close"]}");
                builder.AppendLine("<br>");
            }

            Player.PrintToCenterHtml(builder.ToString());
        }
    }

    public override void Close()
    {
        base.Close();
        RemoveOnTickListener();
        Player.PrintToCenterHtml(" ");
    }

    private void RemoveOnTickListener()
    {
        CounterStrikeSharp.API.Core.Listeners.OnTick handler = Display;
        _plugin.RemoveListener("OnTick", handler);
    }
}

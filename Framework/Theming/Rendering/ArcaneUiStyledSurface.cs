using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Framework.Theming.Rendering
{
    // Applies a semantic palette role without replacing the Button/Dropdown/etc.
    // component or interfering with its EventSystem state.
    internal sealed class ArcaneUiStyledSurface : MonoBehaviour
    {
        internal ArcaneUiSurfaceRole Role;
        internal Color Original;
        internal Image Image;
        internal void Refresh()
        {
            if (Image != null) Image.color = ArcaneUiStyleService.ManagerColor(Role, Original);
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;

namespace Sector.UI
{
    public enum Expression
    {
        Neutral,
        Happy,
        Sad,
        Bored,
        Surprised,
    }

    // A fixed resident's semi-real portrait (Resources/Art/Cast, made by Tools/Art/cast2.py).
    // Square busts for small slots, tall half-body pictures for events and profiles.
    // Expressions without their own picture show the neutral one.
    public class CastPortrait : VisualElement
    {
        readonly string id;
        readonly bool tall;
        Expression expression = Expression.Neutral;

        public CastPortrait(string residentId, bool tall = false)
        {
            id = residentId;
            this.tall = tall;
            AddToClassList("cast-portrait");
            pickingMode = PickingMode.Ignore;
            style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
            Apply();
        }

        public string ResidentId => id;

        public Expression Expression
        {
            get => expression;
            set
            {
                expression = value;
                Apply();
            }
        }

        Texture2D Load(Expression face)
        {
            string key = face == Expression.Happy ? "happy" : face == Expression.Sad ? "sad" : "neutral";
            return PixelArt.Load($"Cast/{id}_{key}{(tall ? "_tall" : "")}");
        }

        void Apply()
        {
            var texture = Load(expression) ?? Load(Expression.Neutral);
            style.backgroundImage = texture != null ? new StyleBackground(texture) : StyleKeyword.Null;
        }
    }
}

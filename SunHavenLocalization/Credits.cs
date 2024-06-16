using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using xiaoye97;

namespace SunHavenLocalization
{
    public static class Credits
    {
        public static void ShowCredits()
        {
            GameObject canvasGO = new GameObject("CreditsCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var imageGO = new GameObject("Image");
            imageGO.transform.SetParent(canvasGO.transform);
            var image = imageGO.AddComponent<Image>();
            var rt = image.transform as RectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            var tex = ResourceUtils.GetTex("Credits.png");
            var creditsSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            image.sprite = creditsSprite;
        }
    }
}

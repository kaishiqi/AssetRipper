using AssetRipper.Numerics;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_687078895;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.SpriteAtlasData;
using System.Drawing;
using System.Numerics;

namespace AssetRipper.SourceGenerated.Extensions;

public static class SpriteExtensions
{
	/// <summary>
	/// Recalculate the sprite coordinates relative to the atlas page that contains it.
	/// </summary>
	/// <remarks>
	/// Sprite values are relative to the original image (the image it was created from).
	/// Since an atlas shuffles and crops sprite images, those values need to be recalculated.
	/// This only applies when the sprite is actually packed into an atlas. Standalone sprites
	/// keep their serialized values.
	/// </remarks>
	/// <returns><see langword="true"/> if the sprite is packed into the atlas; otherwise, <see langword="false"/>.</returns>
	public static bool GetSpriteCoordinatesInAtlas(this ISprite sprite, ISpriteAtlas? atlas, out RectangleF sAtlasRect, out Vector2 sAtlasPivot, out Vector4 sAtlasBorder)
	{
		if (atlas is null
			|| !sprite.Has_RenderDataKey()
			|| !atlas.RenderDataMap.TryGetValue(sprite.RenderDataKey, out ISpriteAtlasData? atlasData))
		{
			// This sprite is not packed in an atlas, so its serialized values are already correct.
			sAtlasRect = sprite.Rect.CastToStruct();
			sAtlasPivot = GetPivot(sprite);
			sAtlasBorder = sprite.Has_Border() ? sprite.Border.CastToStruct() : default;
			return false;
		}

		sAtlasRect = atlasData.TextureRect.CastToStruct();
		Vector2 cropBotLeft = (Vector2)atlasData.TextureRectOffset;

		Vector2 sizeDelta = sprite.Rect.Size() - sAtlasRect.Size();
		Vector2 cropTopRight = new Vector2(sizeDelta.X - cropBotLeft.X, sizeDelta.Y - cropBotLeft.Y);

		Vector2 pivot = GetPivot(sprite);
		Vector2 pivotPosition = new Vector2(pivot.X * sprite.Rect.Size().X, pivot.Y * sprite.Rect.Size().Y);
		Vector2 aAtlasPivotPosition = pivotPosition - cropBotLeft;
		sAtlasPivot = new Vector2(aAtlasPivotPosition.X / sAtlasRect.Size().X, aAtlasPivotPosition.Y / sAtlasRect.Size().Y);

		if (sprite.Has_Border())
		{
			float borderL = sprite.Border.X == 0.0f ? 0.0f : sprite.Border.X - cropBotLeft.X;
			float borderB = sprite.Border.Y == 0.0f ? 0.0f : sprite.Border.Y - cropBotLeft.Y;
			float borderR = sprite.Border.Z == 0.0f ? 0.0f : sprite.Border.Z - cropTopRight.X;
			float borderT = sprite.Border.W == 0.0f ? 0.0f : sprite.Border.W - cropTopRight.Y;
			sAtlasBorder = new Vector4(borderL, borderB, borderR, borderT);
		}
		else
		{
			sAtlasBorder = default;
		}

		return true;
	}

	private static Vector2 GetPivot(ISprite sprite)
	{
		if (sprite.Has_Pivot())
		{
			return (Vector2)sprite.Pivot;
		}
		else
		{
			Vector2 center = new Vector2(sprite.Rect.Size().X / 2.0f, sprite.Rect.Size().Y / 2.0f);
			Vector2 pivotOffset = center + (Vector2)sprite.Offset;
			return new Vector2(pivotOffset.X / sprite.Rect.Size().X, pivotOffset.Y / sprite.Rect.Size().Y);
		}
	}

	public static ITexture2D? TryGetTexture(this ISprite sprite)
	{
		return sprite.RD.Texture.TryGetAsset(sprite.Collection);
	}
}

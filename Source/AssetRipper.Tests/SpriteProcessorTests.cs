using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Processing;
using AssetRipper.Processing.Textures;
using AssetRipper.Primitives;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Extensions;
using System.Drawing;
using System.Numerics;

namespace AssetRipper.Tests;

internal class SpriteProcessorTests
{
	private const float TextureRectX = 32.042908f;
	private const float TextureRectY = 33.026752f;
	private const float TextureRectWidth = 17.93034f;
	private const float TextureRectHeight = 32.89713f;
	private const float Tolerance = 0.0001f;

	[Test]
	public void NonAtlasSpriteKeepsSerializedRect()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		ISprite sprite = CreateTightSprite(collection);

		new SpriteProcessor().Process(CreateGameData(collection));

		using (Assert.EnterMultipleScope())
		{
			Assert.That(sprite.Rect.X, Is.EqualTo(0.0f).Within(Tolerance));
			Assert.That(sprite.Rect.Y, Is.EqualTo(0.0f).Within(Tolerance));
			Assert.That(sprite.Rect.Width, Is.EqualTo(83.0f).Within(Tolerance));
			Assert.That(sprite.Rect.Height, Is.EqualTo(83.0f).Within(Tolerance));
			Assert.That(sprite.Pivot!.X, Is.EqualTo(0.5f).Within(Tolerance));
			Assert.That(sprite.Pivot.Y, Is.EqualTo(0.5f).Within(Tolerance));
			Assert.That(sprite.Offset.X, Is.EqualTo(0.0f).Within(Tolerance));
			Assert.That(sprite.Offset.Y, Is.EqualTo(0.0f).Within(Tolerance));
			Assert.That(sprite.RD.TextureRectOffset.X, Is.EqualTo(TextureRectX).Within(Tolerance));
			Assert.That(sprite.RD.TextureRectOffset.Y, Is.EqualTo(TextureRectY).Within(Tolerance));
		}
	}

	[Test]
	public void NonAtlasSpriteRectIsNotReplacedByTextureRect()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		ISprite sprite = CreateTightSprite(collection);

		new SpriteProcessor().Process(CreateGameData(collection));

		using (Assert.EnterMultipleScope())
		{
			Assert.That(sprite.Rect.Width, Is.Not.EqualTo(sprite.RD.TextureRect.Width).Within(Tolerance));
			Assert.That(sprite.Rect.Height, Is.Not.EqualTo(sprite.RD.TextureRect.Height).Within(Tolerance));
		}
	}

	[Test]
	public void GetSpriteCoordinatesInAtlasWithoutAtlasReturnsFalseAndOriginalValues()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		ISprite sprite = CreateTightSprite(collection);

		bool result = sprite.GetSpriteCoordinatesInAtlas(null, out RectangleF rect, out Vector2 pivot, out Vector4 border);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(result, Is.False);
			Assert.That(rect.X, Is.EqualTo(0.0f).Within(Tolerance));
			Assert.That(rect.Y, Is.EqualTo(0.0f).Within(Tolerance));
			Assert.That(rect.Width, Is.EqualTo(83.0f).Within(Tolerance));
			Assert.That(rect.Height, Is.EqualTo(83.0f).Within(Tolerance));
			Assert.That(pivot.X, Is.EqualTo(0.5f).Within(Tolerance));
			Assert.That(pivot.Y, Is.EqualTo(0.5f).Within(Tolerance));
			Assert.That(border, Is.EqualTo(Vector4.Zero));
		}
	}

	private static ISprite CreateTightSprite(ProcessedAssetCollection collection)
	{
		ISprite sprite = collection.CreateSprite();
		sprite.Rect.X = 0.0f;
		sprite.Rect.Y = 0.0f;
		sprite.Rect.Width = 83.0f;
		sprite.Rect.Height = 83.0f;
		sprite.Offset.X = 0.0f;
		sprite.Offset.Y = 0.0f;
		if (sprite.Has_Pivot())
		{
			sprite.Pivot!.X = 0.5f;
			sprite.Pivot.Y = 0.5f;
		}

		sprite.RD.TextureRect.X = TextureRectX;
		sprite.RD.TextureRect.Y = TextureRectY;
		sprite.RD.TextureRect.Width = TextureRectWidth;
		sprite.RD.TextureRect.Height = TextureRectHeight;
		sprite.RD.TextureRectOffset.X = TextureRectX;
		sprite.RD.TextureRectOffset.Y = TextureRectY;
		return sprite;
	}

	private static GameData CreateGameData(ProcessedAssetCollection collection)
	{
		return new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}
}

// @file: MapGenerator.cpp
//
// @brief: Reads through a jsonFile containing information about a set of TileID's the user wants to generate. 
// An array of TileID's and CollideableTileID's are pulled along with the the texture to pull the tiles from. 
// The MapGenerator calculates the X Y Coordinats on the texture for the tile and compares the TileID to 
// the CollideableTileID's to see if a collider should be added.
//
// @author: Matthew Beaudoin
// @date: 2023-12-15

#include "GameCore.h"
#include "MapGenerator.h"
#include "Sprite.h"

IMPLEMENT_DYNAMIC_CLASS(MapGenerator);

void MapGenerator::Initialize()
{

}

void MapGenerator::Update()
{

}

void MapGenerator::Load(json::JSON& document)
{
	if (document.hasKey("LevelFile"))
	{
		//Opens the jsonFile with the tile information
		std::ifstream inputStream(document["LevelFile"].ToString());
		std::string str((std::istreambuf_iterator<char>(inputStream)), std::istreambuf_iterator<char>());
		json::JSON node = json::JSON::Load(str);

		//The Tile ID's
		if (node.hasKey("Tiles"))
		{
			_tiles = node["Tiles"];
		}
		//Tiles that need to have a collider added
		if (node.hasKey("CollideableID"))
		{
			_tileIDsWithColliders = node["CollideableID"];
		}
		//Texture we pull the Tiles from
		if (node.hasKey("MapTexture"))
		{
			_mapFileGUID = GetHashCode(node["MapTexture"].ToString().c_str());
		}
	}

	if (_tiles.length() > 0 && _mapFileGUID != 0)
	{
		//What index in the TileID array we are at
		int count = 0;

		//Position for the next Tile to be generated at
		float x = 22.4;
		float y = 22.4;

		//Runs through all the rows for Y
		for (int z = 0; z < 16; z++)
		{
			//Runs through all the rows for X
			for (int i = 0; i < 16; i++)
			{
				//Creates a new Entity and sets its position and scale information
				Entity* newTile = ownerEntity->GetParentScene()->CreateEntity();
				newTile->GetTransform().position.x = x;
				newTile->GetTransform().position.y = y;
				newTile->GetTransform().Scale(Vec2(1.4, 1.4));

				//Increments X so the next tile is at the right X position
				x += 44;

				//Creates a sprite component on the new entity and gives it the Dungeon texture
				Sprite* sprite = (Sprite*)newTile->CreateComponent("Sprite");
				sprite->SetTextureAsset((TextureAsset*)AssetManager::Get().GetAsset(_mapFileGUID));

				/*
				* Pulls the TileID from the array and removes 1. Tiled was providing tile id's that were one too high.
				*/
				float tileId = _tiles[count++].ToInt() - 1;

				//Checks if the tile should be given a collider based on its ID
				for (int c = 0; c < _tileIDsWithColliders.length(); c++)
				{
					if (tileId == _tileIDsWithColliders[c].ToInt())
					{
						BoxCollider* collider = (BoxCollider*)newTile->CreateComponent("BoxCollider");
						newTile->SetName("Wall");
						break;
					}
				}

				//Performs math to grab the X and Y coordinates from the TileID
				float textureCorX = ((int)tileId % 20);
				float textureCorY = tileId - textureCorX;
				textureCorX *= 32;
				textureCorY = (textureCorY / 20) * 32;

				//Sets the sprite rect so it only displays the texture at the given coordinates
				SDL_Rect tileRect = { textureCorX, textureCorY, 32.0, 32.0 };
				sprite->SetSourceRect(tileRect);
			}
			//Resets X since we are going into a new X row
			x = 22.4;
			y += 44;
		}
	}
}
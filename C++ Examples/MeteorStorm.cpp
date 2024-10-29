#include "MeteorStorm.h"

MeteorStorm::MeteorStorm()
{

}

void MeteorStorm::Initialize(UI* ui)
{
	//Stores the UI Object from the Render Manager for ease of Access
	_ui = ui;
}

MeteorStorm::~MeteorStorm()
{

}

void MeteorStorm::Destroy()
{
	//Iterates through the Meteors Vector to clear the memory
	for (int i = 0; i < _meteorCountMax; i++)
	{
	
		if (_meteors[i] != NULL)
		{
			DestroyMeteor(i);
		}
	}

	SDL_DestroyTexture(_bigMeteorTexture);
	SDL_DestroyTexture(_smallMeteorTexture);
}

void MeteorStorm::Update(Player* player, float* deltaTime)
{
	/*
	* MeteorStorm checks if enough time has passed between spawns and that we aren't exceeding
	* the max Meteor count. It then iterates through the Meteor Vector to look for a NULL spot 
	* and randomly decides if it will generate a Big or a Small Meteor
	*/

	if (_currentMeteors < _meteorCountMax && _spawnTimer <= 0)
	{
		//Iterates till it finds a NULL position
		for (int i = 0; i < _meteorCountMax; i++)
		{
			if (_meteors[i] == NULL)
			{
				_meteors[i] = new Meteor();

				//Randomly decides if a meteor will be Big or Small 
				if (rand() % 2 == 0)
				{
					_meteors[i]->Initalize(_bigMeteorTexture, 1, rand() % 20 + 15);
				}
				else
				{
					_meteors[i]->Initalize(_smallMeteorTexture, 2, rand() % 20 + 15);
				}

				_currentMeteors++;
				break;
			}
		}
		_spawnTimer = _spawnTimerMax;
	}
	else if (_spawnTimer > 0)
	{
		_spawnTimer = _spawnTimer - (1 * (*deltaTime));
	}

	
	/*
	* The Meteor Vector is iterated through to update the Meteors then check
	* if they have collided with a Player Bullet, The Player themselves or moved 
	* far enough off screen to despawn.
	*/

	for (int i = 0; i < _meteorCountMax; i++)
	{
		//Checks the current position isn't NULL
		if (_meteors[i] != NULL)
		{
			_meteors[i]->Update(deltaTime);

			//Checks if the Meteor has collided with a Player Bullet
			if (player->CheckCollisionBullet(_meteors[i]->GetRect()))
			{
				_ui->IncreaseScore(_meteorScoreValue);
				DestroyMeteor(i);
			}

			//Checks if the Meteor has collied with the Player
			else if (player->CheckCollisionPlayer(_meteors[i]->GetRect()))
			{
				_ui->IncreaseScore(_meteorScoreValue);
				DestroyMeteor(i);
			}

			//Checks if a Meteor have moved significantly off screen
			else if (_meteors[i]->GetPositionY() >= 720)
			{
				DestroyMeteor(i);
			}
		}
	}
}

void MeteorStorm::InitialLoad(json::JSON* document, SDL_Renderer* renderer)
{
	/*
	* Loads the MeteorStorm information from the JSON Object it was passed.
	* Then it generates the textures for the Meteors and makes the Meteor
	* Vector the right size based on the MeteorCount given. 
	*/

	if ((*document).hasKey("BigMeteorTextureName"))
	{
		_bigMeteorTextureName = (*document)["BigMeteorTextureName"].ToString();
	}

	if ((*document).hasKey("SmallMeteorTextureName"))
	{
		_smallMeteorTextureName = (*document)["SmallMeteorTextureName"].ToString();
	}

	if ((*document).hasKey("MeteorCount"))
	{
		_meteorCountMax = (*document)["MeteorCount"].ToInt();
	}

	if ((*document).hasKey("MeteorScoreValue"))
	{
		_meteorScoreValue = (*document)["MeteorScoreValue"].ToInt();
	}

	//Generates the Texture for the Big Meteor
	SDL_Surface* bigMeteorSurface = IMG_Load(_bigMeteorTextureName.c_str());
	_bigMeteorTexture = SDL_CreateTextureFromSurface(renderer, bigMeteorSurface);
	SDL_FreeSurface(bigMeteorSurface);

	//Generates the Texture for the Small Meteor
	SDL_Surface* smallMeteorSurface = IMG_Load(_smallMeteorTextureName.c_str());
	_smallMeteorTexture = SDL_CreateTextureFromSurface(renderer, smallMeteorSurface);
	SDL_FreeSurface(smallMeteorSurface);

	//Creates the needed spaces in the Vector based on the size of the MeteorStorm provided in the JSON file
	for (int i = 0; i < _meteorCountMax; i++)
	{
		_meteors.push_back(nullptr);
	}
}

void MeteorStorm::Save(json::JSON* document)
{
	json::JSON subObject = json::JSON::Object();

	//Saves the Meteor Storm information into the JSON Object
	subObject["CurrentMeteors"] = _currentMeteors;
	subObject["SpawnTimer"] = _spawnTimer;

	json::JSON meteorArray = json::JSON::Array();

	//Iterates through the Meteors Vector to store the information into the JSON Array
	for (int i = 0; i < _meteorCountMax; i++)
	{
		if (_meteors[i] != NULL)
		{
			_meteors[i]->Save(&meteorArray);
		}
	}
	subObject["Meteors"] = meteorArray;

	//Adds the Meteor Storm JSON Object to the JSON Object which was passed up from the Engine
	(*document)["MeteorStorm"] = subObject;
}

void MeteorStorm::SaveDataLoad(json::JSON* document)
{
	/*
	* The MeteorStorm loads its information from the provided JSON Object. It sets its
	* CurrentMeteor count then looks at the Array of saved Meteors. It uses the TextureNum 
	* to know if it should initalize a Big or Small Meteor then uses set methods to set the
	* rest of the Meteors information.
	*/

	if ((*document).hasKey("CurrentMeteors"))
	{
		_currentMeteors = (*document)["CurrentMeteors"].ToInt();
	}

	if ((*document).hasKey("Meteors"))
	{
		json::JSON subArray = ((*document))["Meteors"];

		int i = 0;
		//Iterates through the Array of saved Meteors
		for (json::JSON subObject : subArray.ArrayRange())
		{
			if (_meteors[i] == NULL)
			{
				if (subObject.hasKey("TextureNum"))
				{
					//Looks at TextureNum to know if the Meteor was Big or Small
					int textureNum = subObject["TextureNum"].ToInt();

					_meteors[i] = new Meteor();

					if (textureNum == 1)
					{
						_meteors[i]->Initalize(_bigMeteorTexture, textureNum, rand() % 20 + 15);
					}
					else
					{
						_meteors[i]->Initalize(_smallMeteorTexture, textureNum, rand() % 20 + 15);
					}
				}
			}

			//Sets the rest of the Meteors information

			if (subObject.hasKey("PositionX"))
			{
				_meteors[i]->SetPositionX(subObject["PositionX"].ToFloat());
			}

			if (subObject.hasKey("PositionY"))
			{
				_meteors[i]->SetPositionY(subObject["PositionY"].ToFloat());
			}

			if (subObject.hasKey("Rotation"))
			{
				_meteors[i]->SetRotation(subObject["Rotation"].ToFloat());
			}

			if (subObject.hasKey("SpinSpeed"))
			{
				_meteors[i]->SetSpinSpeed(subObject["SpinSpeed"].ToFloat());
			}

			i++;
		}
	}
}

void MeteorStorm::Render(SDL_Renderer* renderer)
{
	//Iterates through the list of Meteors to render them
	for (int i = 0; i < _meteorCountMax; i++)
	{
		//If a Meteor isn't NULL it will be renderered
		if (_meteors[i] != NULL)
		{
			_meteors[i]->Render(renderer);
		}
	}
}

void MeteorStorm::DestroyMeteor(int i)
{
	//Destroys a Meteor in the Meteors Vector based on the given index
	_currentMeteors--;
	_meteors[i]->Destroy();
	delete _meteors[i];
	_meteors[i] = nullptr;
}

#include "EnemyManager.h"

EnemyManager::EnemyManager()
{

}

void EnemyManager::Initialize()
{

}

EnemyManager::~EnemyManager()
{

}

void EnemyManager::Destroy()
{
	//Iterates through the Enemy Ship Vector to clear the memory
	for (std::list<EnemyShip*>::iterator it = _enemies.begin(); it != _enemies.end();)
	{
		(*it)->Destroy();
		delete (*it);
		_enemies.erase(it++);
	}

	//Checks if the UFO is NULL and if not it clears the memory
	if (_enemyUFO != NULL)
	{
		_enemyUFO->Destroy();
		delete _enemyUFO;
		_enemyUFO = nullptr;
	}

	SDL_DestroyTexture(_enemyShipTexture);
	SDL_DestroyTexture(_enemyShipBulletTexture);
	SDL_DestroyTexture(_enemyUFOTexture);
}

void EnemyManager::Update(SDL_Renderer* renderer, float* deltaTime, Player* player, UI* ui)
{
	/*
	* Spawns new enemies. Makes sure enough time has passed between spawns and that
	* the spawnLimit hasn't been passed. Randomly decides if it will Spawn the standard EnemyShip
	* or the spawner type enemy the EnemyUFO. EnemyShips are stored in a list while EnemyUFO
	* is given its own variable since only one will spawn at a time.
	*/

	if (_enemySpawnTimer <= 0 && _enemyCount < _enemySpawnerLimit)
	{
		//Randomly decides if it will spawn EnemyShip or EnemyUFO
		int choice = rand() % 20;
		if (choice >= 14)
		{
			if (_enemyUFO == nullptr)
			{
				_enemyUFO = new EnemyUFO();
				_enemyUFO->Initialize(_enemyUFOTexture);
				_enemyCount++;
				_enemySpawnTimer = _enemySpawnTimerMax;
			}
		}
		else
		{
			_enemies.push_back(new EnemyShip());
			_enemies.back()->Initialize(_enemyShipTexture, _enemyShipBulletTexture, 0, 0, false, true);
			_enemyCount++;
			_enemySpawnTimer = _enemySpawnTimerMax;
		}
	}
	else if(_enemySpawnTimer > 0 && _enemyCount < _enemySpawnerLimit)
	{
		_enemySpawnTimer -= (100 * (*deltaTime));
	}

	/*
	* Iterates through the List of EnemyShips. The First iterator Updates the Ships. The next checks
	* if the EnemyShip has collided with a Player Bullet. The Third checks if an EnemyShip has collided
	* with the Player. The Last checks if the EnemyShip has moved off screen and needs destroyed.
	*/

	//Updates the EnemyShips
	for (std::list<EnemyShip*>::iterator it = _enemies.begin(); it != _enemies.end(); it++)
	{
		(*it)->Update(player, deltaTime);
	}

	//Checks if EnemyShips have collided with a Player Bullet
	for (std::list<EnemyShip*>::iterator it = _enemies.begin(); it != _enemies.end();)
	{
		if (player->CheckCollisionBullet((*it)->GetRect()))
		{
			/*
			* Checks if the EnemyShip was spawned by the UFO. Ships spanwed by the UFO
			* don't count towards the enemy counter.
			*/

			if (!(*it)->GetUFOSpawned())
			{
				_enemyCount--;
			}

			ui->IncreaseScore(_enemyShipScoreValue);

			(*it)->Destroy();
			delete (*it);
			_enemies.erase(it++);
		}
		else
		{
			it++;
		}
	}

	//Checks if the EnemyShips have collided with the Player
	for (std::list<EnemyShip*>::iterator it = _enemies.begin(); it != _enemies.end();)
	{
		
		if (player->CheckCollisionPlayer((*it)->GetRect()))
		{
			/*
			* Checks if the EnemyShip was spawned by the UFO. Ships spanwed by the UFO
			* don't count towards the enemy counter.
			*/

			if (!(*it)->GetUFOSpawned())
			{
				_enemyCount--;
			}

			ui->IncreaseScore(_enemyShipScoreValue);

			(*it)->Destroy();
			delete (*it);
			_enemies.erase(it++);
		}
		else
		{
			it++;
		}
	}

	//Checks if the EnemyShips have moved far enough off screen to be destroyed
	for (std::list<EnemyShip*>::iterator it = _enemies.begin(); it != _enemies.end();)
	{
		if ((*it)->GetPositionY() >= 720)
		{
			/*
			* Checks if the EnemyShip was spawned by the UFO. Ships spanwed by the UFO
			* don't count towards the enemy counter.
			*/

			if (!(*it)->GetUFOSpawned())
			{
				_enemyCount--;
			}

			(*it)->Destroy();
			delete (*it);
			_enemies.erase(it++);
		}
		else
		{
			it++;
		}
	}

	/*
	* Checks if the EnemyUFO is active. If the UFO is active it is Updated and then 
	* its checked if it has collided with a Player Bullet or the Player themselves. 
	* The EnemyUFO has health so it needs to be checked if it has ran out. The UFO is 
	* also able to spawn in little ships. Its update returns true or false to see if it
	* will spawn a new ship.
	*/

	if (_enemyUFO != NULL)
	{
		//Updates the EnemyUFO and checks if it will spawn a new ship
		if (_enemyUFO->Update(deltaTime))
		{
			_enemies.push_back(new EnemyShip());
			_enemies.back()->Initialize(_enemyUFOTexture, _enemyShipBulletTexture,
				_enemyUFO->GetPositionY() + 20, _enemyUFO->GetPositionX(), true, false);
			_enemies.back()->SetSizing(30);
		}

		//Checks if the UFO collided with a Player Bullet
		if (player->CheckCollisionBullet(_enemyUFO->GetRect()))
		{
			if (_enemyUFO->ReduceHealth() < 0)
			{
				ui->IncreaseScore(_enemyUFOScoreValue);
				DestroyEnemyUFO();
			}
		}
		
		//Checks if the UFO collided with the Player
		if (_enemyUFO != NULL)
		{
			if (player->CheckCollisionPlayer(_enemyUFO->GetRect()))
			{
				if (_enemyUFO->ReduceHealth() < 0)
				{
					ui->IncreaseScore(_enemyUFOScoreValue);
					DestroyEnemyUFO();
				}
			}
		}
	}
}

void EnemyManager::InitialLoad(json::JSON* document, SDL_Renderer* renderer)
{
	/*
	* Recieves a pointer for the renderer and a JSON Object storing
	* the Enemy Managers data from the GameData file. Updates the variables
	* and creates the Enemy textures. 
	*/

	if ((*document).hasKey("EnemyShipTextureName"))
	{
		_enemyShipTextureName = (*document)["EnemyShipTextureName"].ToString();
	}

	if ((*document).hasKey("EnemyShipBulletTextureName"))
	{
		_enemyShipBulletTextureName = (*document)["EnemyShipBulletTextureName"].ToString();
	}

	if ((*document).hasKey("EnemyUFOTextureName"))
	{
		_enemyUFOTextureName = (*document)["EnemyUFOTextureName"].ToString();
	}

	if ((*document).hasKey("EnemyShipScoreValue"))
	{
		_enemyShipScoreValue = (*document)["EnemyShipScoreValue"].ToInt();
	}

	if ((*document).hasKey("EnemyUFOScoreValue"))
	{
		_enemyUFOScoreValue = (*document)["EnemyUFOScoreValue"].ToInt();
	}

	//Generates the texture for the EnemyShip
	SDL_Surface* enemyShipSurface = IMG_Load(_enemyShipTextureName.c_str());
	_enemyShipTexture = SDL_CreateTextureFromSurface(renderer, enemyShipSurface);
	SDL_FreeSurface(enemyShipSurface);

	//Generates the texture for the EnemyShip Bullet
	SDL_Surface* enemyShipBulletSurface = IMG_Load(_enemyShipBulletTextureName.c_str());
	_enemyShipBulletTexture = SDL_CreateTextureFromSurface(renderer, enemyShipBulletSurface);
	SDL_FreeSurface(enemyShipBulletSurface);

	//Generates the texture for the EnemyUFO
	SDL_Surface* enemyUFOSurface = IMG_Load(_enemyUFOTextureName.c_str());
	_enemyUFOTexture = SDL_CreateTextureFromSurface(renderer, enemyUFOSurface);
	SDL_FreeSurface(enemyUFOSurface);
}

void EnemyManager::Save(json::JSON* document)
{
	json::JSON subObject = json::JSON::Object();

	//Adds the EnemyManager information to the JSON Object
	subObject["EnemyCount"] = _enemyCount;
	subObject["EnemySpawnTimer"] = _enemySpawnTimer;

	json::JSON enemyShipArray = json::JSON::Object();

	//Iterates through the EnemyShip list and adds the EnemyShip data to the JSON Array
	for (std::list<EnemyShip*>::iterator it = _enemies.begin(); it != _enemies.end(); it++)
	{
		(*it)->Save(&enemyShipArray);
	}
	subObject["EnemyShips"] = enemyShipArray;

	json::JSON enemyUFO = json::JSON::Object();

	//Adds the _enemyUFO information to the JSON Object
	if (_enemyUFO != NULL)
	{
		_enemyUFO->Save(&enemyUFO);
	}
	subObject["EnemyUFO"] = enemyUFO;

	//Adds the EnemyManager JSON Object to the JSON Object passed up from the Engine
	(*document)["EnemyManager"] = subObject;
}

void EnemyManager::SaveDataLoad(json::JSON* document)
{
	/*
	* Loads the EnemyManager information from the SaveData file. It generates the
	* saved enemy ships using the UFOSpawned bool to tell if the ship was one 
	* spawned by the UFO. It then Spawns the UFO if it was present in the save by checking if
	* one of its key value pairs is present.
	*/

	if ((*document).hasKey("EnemyCount"))
	{
		_enemyCount = (*document)["EnemyCount"].ToInt();
	}

	if ((*document).hasKey("EnemySpawnTimer"))
	{
		_enemySpawnTimer = (*document)["EnemySpawnTimer"].ToInt();
	}

	if ((*document).hasKey("EnemyShips"))
	{
		json::JSON subArray = (*document)["EnemyShips"];

		/*
		* Iterates through the Array to generate the ships. If it was UFO spawned its given a different
		* texture and sizing.
		*/
		for (json::JSON subObject : subArray.ArrayRange())
		{
			if (subObject["UFOSpawned"].ToBool() == true)
			{
				_enemies.push_back(new EnemyShip());
				_enemies.back()->Initialize(_enemyUFOTexture, _enemyShipBulletTexture, 0, 0, true, false);
				_enemies.back()->SetSizing(30);
			}
			else
			{
				_enemies.push_back(new EnemyShip());
				_enemies.back()->Initialize(_enemyShipTexture, _enemyShipBulletTexture, 0, 0, false, true);
			}
			_enemies.back()->SaveDataLoad(&subObject);
		}
	}

	//Checks if the UFO was present in the save then passes the data
	if ((*document).hasKey("EnemyUFO"))
	{
		json::JSON subObject = (*document)["EnemyUFO"];

		if (subObject.hasKey("MovingLeft"))
		{
			_enemyUFO = new EnemyUFO();
			_enemyUFO->Initialize(_enemyUFOTexture);
			_enemyUFO->SaveDataLoad(&subObject);
		}
	}
}

void EnemyManager::Render(SDL_Renderer* renderer)
{
	//Iterates through the EnemyShip list for rendering
	for (std::list<EnemyShip*>::iterator it = _enemies.begin(); it != _enemies.end(); it++)
	{
		(*it)->Render(renderer);
	}

	//Checks if the EnemyUFO is present and renderss it
	if (_enemyUFO != NULL)
	{
		_enemyUFO->Render(renderer);
	}
}

void EnemyManager::DestroyEnemyUFO()
{
	//Clears the UFO and decreases the Enemy Count
	_enemyUFO->Destroy();
	delete _enemyUFO;
	_enemyUFO = nullptr;

	_enemyCount--;
}
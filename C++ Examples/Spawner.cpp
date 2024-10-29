// @file: Spawner.cpp
//
// @brief: Generates new entities, gives them a randomly decidied enemy controller and passes along associated data
//
// @author: Matthew Beaudoin
// @date: 2023-12-13

#include "GameCore.h"
#include "Spawner.h"
#include "Sprite.h"
#include "GhostController.h"
#include "WitchController.h"
#include "SkeletonController.h"
#include "EnemyHealthModule.h"

IMPLEMENT_DYNAMIC_CLASS(Spawner);

void Spawner::Initialize()
{
	//Seeds the random
	srand(time(NULL));
}

void Spawner::Update()
{
	if (_timeBetweenSpawns > 0)
	{
		_timeBetweenSpawns -= 1 * Time::Instance().DeltaTime();
	}
	//Makes sure there spawn limit hasn't been reached and the timer has passed
	else if (_timeBetweenSpawns <= 0 && _enemyCount < _enemyCountMax)
	{
		//Creates a new Entity
		Entity* newEnemy = ownerEntity->GetParentScene()->CreateEntity();

		//Sets the Entities position and name
		newEnemy->GetTransform().position = ownerEntity->GetTransform().position;
		newEnemy->SetName("Enemy");

		//Decides what type of enemy will be created
		int choice = rand() % 3;
		Sprite* enemySprite = (Sprite*)newEnemy->CreateComponent("Sprite");

		//Pointers to point to new generated instances of the enemy types
		GhostController* ghost = nullptr;
		WitchController* witch = nullptr;
		SkeletonController* skeleton = nullptr;

		EnemyHealthModule* enemyHealth = nullptr;
		json::JSON moduleData = json::JSON::Object();

		switch (choice) 
		{
			//Generates a Ghost Enemy. It chases the player and destroys itself on impact
			case 0:
				//Sets the initial sprite to the front facing sprite pulled from the loaded data
				enemySprite->SetTextureAsset((TextureAsset*)AssetManager::Get()
					.GetAsset(GetHashCode(_ghostData["FrontFacingSprite"].ToString().c_str())));

				newEnemy->GetTransform().Scale(Vec2(0.2, 0.2));

				newEnemy->CreateComponent("BoxCollider");

				enemyHealth = (EnemyHealthModule*)newEnemy->CreateComponent("EnemyHealthModule");
				moduleData = _ghostData["HealthModuleData"];
				enemyHealth->Load(moduleData);
				enemyHealth->SetSpawner((Spawner*)ownerEntity->GetComponent("Spawner"));

				ghost = (GhostController*)newEnemy->CreateComponent("GhostController");
				//Passes along the Ghost data
				ghost->Load(_ghostData);

				break;
			//Generates a Witch enemy. It stands far away and attacks from range
			case 1:
				//Sets the initial sprite to the front facing sprite pulled from the loaded data
				enemySprite->SetTextureAsset((TextureAsset*)AssetManager::Get()
					.GetAsset(GetHashCode(_witchData["FrontFacingSprite"].ToString().c_str())));

				newEnemy->GetTransform().Scale(Vec2(0.4, 0.4));

				newEnemy->CreateComponent("BoxCollider");

				enemyHealth = (EnemyHealthModule*)newEnemy->CreateComponent("EnemyHealthModule");
				moduleData = _witchData["HealthModuleData"];
				enemyHealth->Load(moduleData);
				enemyHealth->SetSpawner((Spawner*)ownerEntity->GetComponent("Spawner"));

				witch = (WitchController*)newEnemy->CreateComponent("WitchController");
				//Passes along the Witch data
				witch->Load(_witchData);

				break;
			//Generates a Skeleton enemy. It chases after the player although stops short and attacks.
			case 2:
				//Sets the initial sprite to the front facing sprite pulled from the loaded data
				enemySprite->SetTextureAsset((TextureAsset*)AssetManager::Get()
					.GetAsset(GetHashCode(_skeletonData["FrontFacingSprite"].ToString().c_str())));

				newEnemy->GetTransform().Scale(Vec2(0.4, 0.4));

				newEnemy->CreateComponent("BoxCollider");

				enemyHealth = (EnemyHealthModule*)newEnemy->CreateComponent("EnemyHealthModule");
				moduleData = _skeletonData["HealthModuleData"];
				enemyHealth->Load(moduleData);
				enemyHealth->SetSpawner((Spawner*)ownerEntity->GetComponent("Spawner"));

				skeleton = (SkeletonController*)newEnemy->CreateComponent("SkeletonController");
				//Passes along Skeleton data
				skeleton->Load(_skeletonData);

				break;
		}
		
		//Resets Timer
		_timeBetweenSpawns = _timeBetweenSpawnsMax;

		//Increases Enemy Count
		_enemyCount++;
	}
}

void Spawner::Load(json::JSON& document)
{
	if (document.hasKey("TimeBetweenSpawns"))
	{
		_timeBetweenSpawnsMax = document["TimeBetweenSpawns"].ToFloat();
	}
	if (document.hasKey("EnemyCountMax"))
	{
		_enemyCountMax = document["EnemyCountMax"].ToInt();
	}

	//Pulls the data for the different enemy types
	if (document.hasKey("GhostData"))
	{
		_ghostData = document["GhostData"];
	}
	if (document.hasKey("WitchData"))
	{
		_witchData = document["WitchData"];
	}
	if (document.hasKey("SkeletonData"))
	{
		_skeletonData = document["SkeletonData"];
	}
}

void Spawner::EnemyDied()
{
	_enemyCount--;
}
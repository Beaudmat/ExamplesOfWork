using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Author: Matthew Beaudoin
 * 
 *  This script was responsible for controlling tower objects in the Hex Chronicles game project I worked on.
 *  The tower would attempt to target a player in range and would mark the selected players area. The following
 *  turn it would fire a projectile at the marked locations.
 */

public class Tower : Spawner
{
    #region Variables

    [Range(0, 4)]
    [SerializeField] private int tileRange = 3;
    private float actualRange;

    public int TileRange
    {
        get { return tileRange; }
    }

    [SerializeField] private int damage = 5;

    [SerializeField] private AttackArea attackAreaPrefab;
    private AttackArea spawnedAttackArea;

    private List<Character> possibleCharacterChoices = new List<Character>();
    List<Tile> tilesToColor = new List<Tile>();

    [SerializeField] private Projectile projectile;
    [SerializeField] private float archHeight;
    [SerializeField] private float projectileSpeed;

    #endregion

    #region UnityMethods

    public override void Start()
    {
        base.Start();

        Debug.Assert(attackAreaPrefab != null, $"{name} doesn't have an AttackArea prefab provided.");

        actualRange = tileRange * 2f;
    }

    #endregion

#region CustomMethods

    /*
     * Checks if the tower is able to attack. If it has already marked an attack area or 
     * a player unit is within range it returns true.
     */
    public bool CanAttack()
    {
        if(spawnedAttackArea != null )
        {
            return true;
        }
        else
        {
            possibleCharacterChoices.Clear();

            //Grabs all player units that are within range
            foreach (Character character in turnManager.characterList)
            {
                if (Vector3.Distance(character.transform.position, transform.position) <= actualRange)
                {
                    possibleCharacterChoices.Add(character);
                }
            }

            //If a unit was found in range return true
            if(possibleCharacterChoices.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    /*
     * Attempts to perform an attack either by marking the area it will attack or
     * launching projectiles at the previously marked area.
     */
    public void AttemptAttack()
    {
        //Clears out any characters in its attack area that may have been destroyed
        List<Character> charactersToRemove = new List<Character>();
        foreach(Character character in possibleCharacterChoices)
        {
            if(character ==  null)
            {
                charactersToRemove.Add(character);
            }
        }

        foreach(Character character in charactersToRemove)
        {
            possibleCharacterChoices.Remove(character);
        }

        //Performs if the tower has not marked an attack area yet
        if(spawnedAttackArea == null)
        {
            if(possibleCharacterChoices.Count > 0)
            {
                //Chooses a character to target for attack
                int choice = Random.Range(0, possibleCharacterChoices.Count);
                Vector3 spawnChoice = possibleCharacterChoices[choice].transform.position;
                spawnChoice.y = 0;

                //Spawns in the towers attack area colliders
                spawnedAttackArea = Instantiate(attackAreaPrefab, spawnChoice, Quaternion.identity);
                turnManager.mainCameraController.MoveToTargetPosition(spawnedAttackArea.transform.position, true);

                //Colors the tiles effected by the attack
                Tile originTile = possibleCharacterChoices[choice].characterTile;
                tilesToColor = new List<Tile>(turnManager.pathfinder.FindAdjacentTiles(originTile, true))
                {
                    originTile
                };

                foreach (Tile tile in tilesToColor)
                {
                    tile.ChangeTileEffect(TileEnums.TileEffects.towerAttack, true);
                }
            }
        }
        //Performs if the tower previously marked an attack area
        else
        {
            turnManager.mainCameraController.MoveToTargetPosition(spawnedAttackArea.transform.position, true);

            //Launches projectiles at the tiles effected by the attack
            spawnedAttackArea.DetectArea(false, false);
            foreach(Tile tile in tilesToColor)
            {
                Projectile newProjectile = Instantiate(projectile, transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity);
                if(tile.tileOccupied)
                {
                    newProjectile.Launch(tile.transform.position, archHeight, projectileSpeed, tile.characterOnTile, damage);
                }
                else
                {
                    newProjectile.Launch(tile.transform.position, archHeight, projectileSpeed);
                }
            }

            AudioManager.Instance.PlaySound(tileObjectData.SFX);

            //Removes the attack coloring from the tiles
            foreach (Tile tile in tilesToColor)
            {
                tile.ChangeTileEffect(TileEnums.TileEffects.towerAttack, false);
            }

            spawnedAttackArea.DestroySelf();
            spawnedAttackArea = null;
        }
    }

    /*
     * Called whenever a player unit attacks the tower object
     */
    public override void TakeDamage(float attackDamage)
    {
        currentHealth -= attackDamage;

        UpdateHealthBar?.Invoke();

        DamageText damageText = Instantiate(damagePrefab, transform.position, Quaternion.identity).GetComponent<DamageText>();
        damageText.ShowDamage(attackDamage);

        if (currentHealth <= 0)
        {
            //If the tower has marked an area for attack its cleaned up
            if(spawnedAttackArea != null)
            {
                foreach (Tile tile in tilesToColor)
                {
                    tile.ChangeTileEffect(TileEnums.TileEffects.towerAttack, false);
                }
                spawnedAttackArea.DestroySelf();
            }

            objectDestroyed.Invoke(this);

            attachedTile.objectOnTile = null;
            attachedTile.tileHasObject = false;

            Destroy(gameObject);
        }
    }

    /*
     * Called by the undo system to store information about where the tower
     * planned to attack if it had marked an area.
     */
    public UndoData_Tower CustomUndoData()
    {
        UndoData_Tower data = new UndoData_Tower();
        if (spawnedAttackArea != null)
        {
            data.attacking = true;
            data.attackAreaPosition = spawnedAttackArea.transform.position;
        }
        else
        {
            data.attacking = false;
        }

        return data;
    }

    /*
     * Called by the undo system to restore the area the tower planned to attack
     * if the tower had been destroyed then the undo button was pressed.
     */
    public override void Undo(UndoData_TileObjCustomInfo data)
    {
        if(spawnedAttackArea == null)
        {
            UndoData_Tower towerData = (UndoData_Tower)data;

            //Spawns in the attack colliders
            spawnedAttackArea = Instantiate(attackAreaPrefab, towerData.attackAreaPosition, Quaternion.identity);

            //Colors the effected tiles
            foreach (Tile tile in tilesToColor)
            {
                tile.ChangeTileEffect(TileEnums.TileEffects.towerAttack, true);
            }
        }
    }

    #endregion
}

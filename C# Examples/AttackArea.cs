using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Author: Matthew Beaudoin
 * 
 *  This script was responsible for controlling the AttackAreas in the Hex Chronicles game project.
 *  The AttackArea is a collection of colliders that would collide with tiles in the games Hex Grid.
 *  This script was responsible with coloring tiles, positioning colliders, performing effects 
 *  and collecting information about the effected tiles.
 */

public class AttackArea : MonoBehaviour
{
    #region Variables

    private List<TileReporter> tileReporters = new List<TileReporter>();
    private List<Tile> reporterTiles = new List<Tile>();

    public List<TileReporter> originReporters = new List<TileReporter>();

    [Header("Movement: ")]
    [SerializeField] public bool freeRange = false;

    [Header("Tile Targeting: ")]
    [SerializeField] public bool onlySingleTileType = false;
    [SerializeField] public ElementType effectedTileType;

    [Header("Effect Targeting: ")]
    [SerializeField] public bool hitsEnemies = true;
    [SerializeField] public bool hitsHeroes = true;
    [SerializeField] public bool hitsTileObjects = true;

    [Header("Range: ")]
    [SerializeField] public float maxHittableRange = 1f;

    #endregion

    #region UnityMethods

    void Start()
    {
        foreach (TileReporter reporter in transform.GetComponentsInChildren<TileReporter>())
        {
            tileReporters.Add(reporter);
        }

        List<TileReporter> reportersToRemove = new List<TileReporter>();
        foreach(TileReporter reporter in originReporters)
        {
            if (reporter == null)
            {
                reportersToRemove.Add(reporter);
                Debug.LogWarning("(" + transform.parent.name + ") had a null origin reporter.");
            }
        }

        foreach (TileReporter reporter in reportersToRemove)
        {
            originReporters.Remove(reporter);
        }
        reportersToRemove.Clear();
    }

    #endregion

    #region CustomMethods

    private void ResetArea()
    {
        foreach (Tile tile in reporterTiles)
        {
            if(tile == null)
            {
                continue;
            }

            // Reset preview on enemy healthbar
            if (tile.tileOccupied && tile.characterOnTile != null)
            {
                tile.characterOnTile.DonePreview?.Invoke();
            }

            if (tile.tileHasObject && tile.objectOnTile != null)
            {
                tile.objectOnTile.DonePreview?.Invoke();
            }

            tile.ChangeTileEffect(TileEnums.TileEffects.attackable, false);
        }
    }

    //Colors all tiles in its area of effect
    private void ColourArea(bool highlightOccupied)
    {
        foreach (Tile tile in reporterTiles)
        {
            tile.ChangeTileEffect(TileEnums.TileEffects.attackable, true);
        }
    }

    //Resets the area than checks what tiles it is interacting with
    public void DetectArea(bool illustrate, bool highlightOccupied)
    {
        ResetArea();
        reporterTiles.Clear();

        /*
         * Allows objects to block parts of the AttackArea. the AttackAreas colliders reference eachother
         * and the foreach loop runs through the chain of connected colliders checking if one is blocked to mark all
         * the following ones as also blocked.
         */
        if (originReporters != null)
        {
            foreach(TileReporter tileReporter in originReporters)
            {
                if (tileReporter == null)
                {
                    continue;
                }
                tileReporter.CheckBlockages(false);
            }
        }

        //Runs through the tile reporters and stores the tiles from the ones that aren't null
        foreach (TileReporter reporter in tileReporters)
        {
            if(reporter.currentTile != null)
            {
                reporterTiles.Add(reporter.currentTile);
            }
        }

        if (illustrate)
        {
            ColourArea(highlightOccupied);
        }
    }

    //Triggers any additonal effects for the attack shape
    public void ExecuteAddOnEffects()
    {
        foreach(TileReporter reporter in tileReporters)
        {
            reporter.ExecuteAddOnEffect();
        }
    }

    //Returns a list of all Characters in its area based on the provided type
    public List<Character> CharactersHit(TurnEnums.CharacterType type)
    {
        List<Character> characters = new List<Character>();
        foreach (Tile tile in reporterTiles)
        {
            if (tile.tileOccupied && tile.characterOnTile.characterType == type)
            {
                characters.Add(tile.characterOnTile);
            }
        }
        return characters;
    }

    //Returns a list of all objects in its area
    public List<TileObject> ObjectsHit()
    {
        List<TileObject> objects = new List<TileObject>();
        foreach(Tile tile in reporterTiles)
        {
            if(tile.tileHasObject)
            {
                objects.Add(tile.objectOnTile);
            }
        }
        return objects;
    }

    //Returns a list of tiles being effected by the AttackArea
    public List<Tile> TilesHit()
    {
        List<Tile> tileList = new List<Tile>();
        foreach(Tile tile in reporterTiles)
        {
            tileList.Add(tile);
        }
        return tileList;
    }

    //Checks if its area contains a specific tile
    public bool ContainsTile(Tile tileToCheck)
    {
        foreach(Tile tile in reporterTiles)
        {
            if(tileToCheck == tile)
            {
                return true;
            }
        }

        return false;
    }

    //Rotates the AttackArea based on a provided characters tile
    public void PositionAndRotateAroundCharacter(Pathfinder pathfinder, Tile originTile, Tile targetTile)
    {
        Tile selectedTile = null;
        float distance = 1000f;

        //Checks each tile adjacent to the character to determine which is closest to the target
        foreach (Tile tile in pathfinder.FindAdjacentTiles(originTile, true))
        {
            float newDistance = Vector3.Distance(targetTile.transform.position, tile.transform.position);
            if (newDistance < distance)
            {
                selectedTile = tile;
                distance = newDistance;
            }
        }

        Vector3 newPos = selectedTile.transform.position;
        newPos.y = 0;

        transform.position = newPos;
        Rotate(selectedTile, originTile);
    }

    //Rotates based on given tile
    public void Rotate(Tile targetTile, Tile originTile)
    {
        Transform originTransform = originTile.transform;
        Transform tileTransform = targetTile.transform;

        float rotation = originTransform.eulerAngles.y;

        //Determines the angle between the current tile and the target
        float angle = Vector3.Angle(originTransform.forward, (tileTransform.position - originTransform.position));

        //Checks if the rotation should be applied clockwise or counter clockwise
        if (Vector3.Distance(tileTransform.position, originTransform.position + (originTransform.right * 6)) <
            Vector3.Distance(tileTransform.position, originTransform.position + (-originTransform.right) * 6))
        {
            rotation += angle;
        }
        else
        {
            rotation -= angle;
        }

        //Applies the new rotation
        transform.eulerAngles = new Vector3(0, rotation, 0);
    }

    public void DestroySelf()
    {
        ResetArea();
        Destroy(this.gameObject);
    }

    public static AttackArea SpawnAttackArea(AttackArea attackArea, Vector3 position)
    {
        return Instantiate(attackArea, position, Quaternion.identity);
    }

    #endregion
}

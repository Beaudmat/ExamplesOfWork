using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/*
 * Author: Matthew Beaudoin
 * 
 * This script was responsible for displaying information to the player about enemies and towers 
 * movement and attack ranges in the Hex Chronicles game project. When the player selected either 
 * an enemy or tower a glowing edge would be created around the maximum range that the selected 
 * object could potentially move and another around where it could potentially attack. 
 */

public class AttackPreviewer : Singleton<AttackPreviewer>
{
    #region Variables

    private List<Tile> checkedTiles = new List<Tile>();
    private List<GameObject> tileTops = new List<GameObject>();

    private List<Tile> tileObjCheckedTiles = new List<Tile>();
    private List<GameObject> tileObjTileTops = new List<GameObject>();

    private List<Tile> attackTiles = new List<Tile>();

    [Header("Spawn Info: ")]
    [SerializeField] private LayerMask tileLayer;
    [Range(0.1f, 2f)]
    [SerializeField] private float spawnHeight = 1f;

    [Header("Top Prefabs: ")]
    [SerializeField] private GameObject movementTop;
    [SerializeField] private GameObject attackTop;

    [Header("Edges: ")]
    [SerializeField] private GameObject attackEdge;
    [SerializeField] private GameObject moveEdge;

    [Header("Marker Prefabs: ")]
    [SerializeField] private GameObject targetMarker;
    private GameObject spawnedMarker;

    #endregion

    #region UnityMethods

    private void Start()
    {
        Debug.Assert(movementTop != null, "The AttackPreviewer has not been provided a MovementTop Prefab");
        Debug.Assert(attackTop != null, "The AttackPreviewer has not been provided a AttackTop Prefab");
    }

    #endregion

    #region CustomMethods

    //Spawns in tops to visualize the area an Enemy can Move or Attack during its next turn
    public void PreviewMoveAttackArea(Enemy_Base enemy)
    {
        ClearAttackArea();

        if(Status.GrabIfStatusActive(enemy, Status.StatusTypes.Bound) != null)
        {
            List<Tile> originTile = new List<Tile>()
            {
                enemy.characterTile
            };
            ProduceEdges(enemy.characterTile, originTile, tileTops, moveEdge);

            return;
        }

        PreviewOrigin previewArea = null;
        List<TileReporter> pointsToCheck = null;

        if (enemy.attackAreaPreview != null)
        {
            previewArea = Instantiate(enemy.attackAreaPreview);
            pointsToCheck = previewArea.GetComponentsInChildren<TileReporter>().ToList();
        }

        //Grabs the movement tiles for the Enemy
        List<Tile> tiles = new List<Tile>(Pathfinder.Instance.ReturnMovementTiles(enemy));
        checkedTiles = new List<Tile>(tiles);

        foreach(Tile tile in tiles)
        {
            ProduceEdges(tile, tiles, tileTops, moveEdge);

            //If a movement tile is on the edge of the movement range it checks for attacking tops
            if(tile.cost >= enemy.moveDistance && previewArea != null)
            {
                CheckAttackSpawns(tile, previewArea, pointsToCheck);
            }
        }

        List<Tile> avoidTiles = new List<Tile>(tiles);
        foreach(Tile tile in attackTiles)
        {
            avoidTiles.Add(tile);
        }

        foreach(Tile tile in attackTiles)
        {
            ProduceEdges(tile, avoidTiles, tileTops, attackEdge);
        }
        attackTiles.Clear();

        //Spawns an indicator over the cloest enemy to display who the enemy is likely to target
        Character closestCharacter = enemy.LikelyTarget();

        if(closestCharacter != null)
        {
            spawnedMarker = Instantiate(targetMarker, closestCharacter.transform.position, Quaternion.identity);
        }
    }

    //Checks for any tiles that can be attacked from the provided edge tile
    private void CheckAttackSpawns(Tile tile, PreviewOrigin previewArea, List<TileReporter> pointsToCheck)
    {
        previewArea.transform.position = tile.transform.position + new Vector3(0, 1, 0);
        float rotation = 0;

        //Checks for attacking from each rotation on a hexagon
        for (int i = 0; i < 6; i++)
        {
            previewArea.transform.eulerAngles = new Vector3(0, rotation, 0);

            foreach(TileReporter point in pointsToCheck)
            {
                //Checks if the tile at the point needs an attack top
                ConfirmAttackTopSpawn(point);
            }
            //Spawns in the attack tops
            StoreAttackTiles(previewArea, pointsToCheck);

            rotation += 60;
        }
    }

    //Uses a Ray to detect if the current point is over a tile needing an attack top
    private void ConfirmAttackTopSpawn(TileReporter point)
    {
        if (Physics.Raycast(point.transform.position, Vector3.down, out RaycastHit hit, spawnHeight + 0.9f, tileLayer))
        {
            Tile hitTile = hit.transform.GetComponent<Tile>();

            if (!hitTile.tileHasObject && !checkedTiles.Contains(hitTile))
            {
                point.currentTile = hitTile;
            }
        }
    }

    //Spawns in the attack tops and checks for any bloackages from tileObjects interupting the enemies attack
    private void StoreAttackTiles(PreviewOrigin previewArea, List<TileReporter> pointsToCheck)
    {
        //Checks for any blockages
        previewArea.originTile.CheckBlockages(false);

        //Spawns tops
        foreach (TileReporter point in pointsToCheck)
        {
            if (point.currentTile != null)
            {
                Vector3 position = point.transform.position;
                position.y = spawnHeight;

                attackTiles.Add(point.currentTile);
                checkedTiles.Add(point.currentTile);
            }
        }

        //Resets the TileReporters
        previewArea.originTile.CheckBlockages(true);
    }

    public void ClearAttackArea()
    {
        foreach(GameObject top in tileTops)
        {
            Destroy(top);
        }

        checkedTiles.Clear();
        tileTops.Clear();

        if (spawnedMarker != null)
        {
            Destroy(spawnedMarker);
        }
    }

    public void PreviewAttackAreaTower(Tower tileObj)
    {
        List<Tile> attackTiles = Pathfinder.Instance.ReturnRange(tileObj.attachedTile, tileObj.TileRange);

        foreach (Tile tile in attackTiles)
        {
            ProduceEdges(tile, attackTiles, tileTops, attackEdge);
        }
    }

    public void ProduceEdges(Tile origin, List<Tile> tileList, List<GameObject> listToAddOnto, GameObject edgeObject)
    {
        if (origin == null)
        {
            return;
        }

        Vector3 direction = Vector3.forward;
        float rayLength = 50f;
        float rayHeightOffset = 1f;

        //Checks in all 6 direction for an adjacent tile
        for (int i = 0; i < 6; i++)
        {
            direction = Quaternion.Euler(0f, 60f, 0f) * direction;

            Vector3 aboveTilePos = origin.transform.position + direction;
            aboveTilePos.y += rayHeightOffset;

            //If we hit a tile we add it to the list of adjacents
            if (Physics.Raycast(aboveTilePos, Vector3.down, out RaycastHit hit, rayLength, tileLayer))
            {
                Tile hitTile = hit.transform.GetComponent<Tile>();
                if (!tileList.Contains(hitTile))
                {
                    GameObject newEdge = Instantiate(edgeObject, origin.transform.position + new Vector3(0, spawnHeight, 0), Quaternion.identity);
                    listToAddOnto.Add(newEdge);
                    newEdge.transform.eulerAngles = new Vector3(0, 60f * (i + 1), 0);
                }
            }
            else
            {
                GameObject newEdge = Instantiate(edgeObject, origin.transform.position + new Vector3(0, spawnHeight, 0), Quaternion.identity);
                listToAddOnto.Add(newEdge);
                newEdge.transform.eulerAngles = new Vector3(0, 60f * (i + 1), 0);
            }
        }
    }

    #endregion
}

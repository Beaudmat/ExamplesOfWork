using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*
 * Author: Matthew Beaudoin
 * 
 * This script was part of the AI for the enemies in the Hex Chronicles game project I worked on
 * that used a hex grid system. Enemy AI would look at all the tiles it could reach this turn then
 * examine the value of moving to each of those tiles and attacking in each direction to create 
 * MoveAttackCombos. Five would be stored at a time and if new combos were found with values
 * higher then some already stored a selection chance would occur to switch them out. At the end
 * a final selection is made on what one to perform.
 */

public class EnemyBrain : MonoBehaviour
{
    #region Variables

    [SerializeField] private TurnManager turnManager;

    Dictionary<int, MoveAttackCombo> combinations = new Dictionary<int, MoveAttackCombo>();

    List<int> inThreatKeys = new List<int>();

    private int uniqueIds = 0;
    private int lowestValue = 100;

    private Vector3 nullVector = Vector3.back;

    public bool DecisionMakingFinished { get; private set; }

    #endregion

    #region UnityMethods

    void Start()
    {
        Debug.Assert(turnManager != null, "EnemyBrain doesn't have the TurnManager set");
        nullVector *= 20;
        DecisionMakingFinished = true;
    }

    #endregion

    #region CustomMethods

    public void CalculateEnemyTurns()
    {
        if (DecisionMakingFinished)
        {
            DecisionMakingFinished = false;
            uniqueIds = 0;
            StartCoroutine(EnemiesUpdate());
        }
    }

    private IEnumerator EnemiesUpdate()
    {
        yield return new WaitForSeconds(1f);
        Enemy_Base[] enemies = turnManager.enemyList.ToArray();

        //Runs through each enemy to perform their turn
        foreach (Enemy_Base enemy_base in enemies)
        {
            yield return null;

            //Checks that the current enemy hasn't been destroyed.
            if (enemy_base == null && !turnManager.enemyList.Contains(enemy_base))
            {
                continue;
            }

            Debug.Log(enemy_base.name + " is thinking...");

            turnManager.mainCameraController.FollowTarget(enemy_base.transform, true);

            //Checks if an enemy is bound and therefore can't move
            if(Status.GrabIfStatusActive(enemy_base, Status.StatusTypes.Bound) != null)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            //Performs any calculations an enemy must perform before it decides on its combo
            enemy_base.PreCalculations(turnManager);

            AttackArea enemyAttackArea = AttackArea.SpawnAttackArea(enemy_base.basicAttackArea, enemy_base.transform.position);
            turnManager.mainCameraController.FollowTarget(enemy_base.transform, true);

            //Calculates the tiles the character can move onto
            turnManager.pathfinder.ResetPathFinder();
            turnManager.pathfinder.FindMovementPathsCharacter(enemy_base, false);

            List<Tile> usableTiles = new List<Tile>(turnManager.pathfinder.frontier)
            {
                enemy_base.characterTile,
            };
            
            //Checks if the enemy has the mindcontrol status
            bool mindControl = (Status.GrabIfStatusActive(enemy_base, Status.StatusTypes.MindControl) != null);

            Character currentClosest = null;
            float charDistance = 1000f;

            List<Character> charactersToCheck;
            //If not mind controlled the player unit list is grabbed. 
            if(!mindControl)
            {
                charactersToCheck = turnManager.characterList;
            }
            //If mind controlled the enemy unit list is grabbed
            else
            {
                charactersToCheck = turnManager.enemyList.ToList<Character>();
            }

            //Checks what character is closest that the enemy will attempt to target
            foreach (Character character in charactersToCheck)
            {
                if (character == enemy_base)
                {
                    continue;
                }

                float newDistance = Vector3.Distance(character.transform.position, enemy_base.transform.position);

                if (newDistance < charDistance)
                {
                    charDistance = newDistance;
                    currentClosest = character;
                }
            }

            //Runs through all the movement tiles
            foreach (Tile tile in usableTiles)
            {
                //Makes sure the tile isn't occupied
                if(tile.tileOccupied || tile.tileHasObject)
                {
                    continue;
                }

                //Checks how valuable an enemy finds the current tile for moving
                int valueOfCombination = enemy_base.CalculateMovementValue(tile, enemy_base, turnManager, currentClosest);

                bool checkAttacks = false;

                //Checks if the closest character doesn't exit
                if(currentClosest == null)
                {
                    continue;
                }

                float newDistance = Vector3.Distance(currentClosest.transform.position, tile.transform.position);

                //Checks if we should analyzing potential attack options
                if (newDistance <= enemyAttackArea.maxHittableRange)
                {
                    checkAttacks = true;
                }

                if (checkAttacks == true)
                {
                    //Runs through all the Tiles adjacent to the current Movement tile
                    foreach (Tile adjacentTile in turnManager.pathfinder.FindAdjacentTiles(tile, true))
                    {
                        //Positions and Rotates the AttackArea object
                        enemyAttackArea.transform.position = adjacentTile.transform.position;
                        Vector3 rotation = DetermineAttackAreaRotation(adjacentTile, tile);
                        enemyAttackArea.transform.eulerAngles = rotation;

                        //Calculates the value of Attacking in that direction, IMPORTANT YIELD which lets the triggers update
                        yield return new WaitForSeconds(0.02f);
                        enemyAttackArea.DetectArea(false, false);

                        //If the attack won't hit any players the rotation value is set to the nullvector to mark it as non attacking
                        if (!mindControl && enemyAttackArea.CharactersHit(TurnEnums.CharacterType.Player).Count == 0)
                        {
                            rotation = nullVector;
                        }
                        else if(mindControl && enemyAttackArea.CharactersHit(TurnEnums.CharacterType.Enemy).Count == 0)
                        {
                            rotation = nullVector;
                        }

                        //If the attack is valid it checks how valuable the enemy finds attacking in that direction to be
                        if(rotation != nullVector)
                        {
                            valueOfCombination += enemy_base.CalculteAttackValue(enemyAttackArea, turnManager, tile);
                        }

                        if (combinations.Count < 5)
                        {
                            AddCombination(tile, adjacentTile, rotation, valueOfCombination);
                        }
                        else
                        {
                            CheckIfComboKeep(tile, adjacentTile, rotation, valueOfCombination);
                        }
                    }
                }
                else
                {
                    if(combinations.Count < 5)
                    {
                        AddCombination(tile, tile, nullVector, valueOfCombination);
                    }
                    else
                    {
                        CheckIfComboKeep(tile, tile, nullVector, valueOfCombination);
                    }
                }
            }

            int id = -1;
            //Picks the final choice to execute
            if (!IfAttacksClearNon())
            {
                int highestValue = -100;

                foreach(KeyValuePair<int, MoveAttackCombo> comboValues in combinations)
                {
                    if(comboValues.Value.value > highestValue)
                    {
                        id = comboValues.Key;
                        highestValue = comboValues.Value.value;
                    }
                }
            }

            int combinationChoice = 0;
            int comboValue = 0;
            if(id != -1)
            {
                foreach(KeyValuePair<int, MoveAttackCombo> comboValues in combinations)
                {
                    if(comboValues.Value.value > comboValue)
                    {
                        combinationChoice = comboValues.Key;
                        comboValue = comboValues.Value.value;
                    }
                }
            }
            else
            {
                combinationChoice = PickCombination();
            }

            //Executes the Movement portion than the Action portion on movement completion
            if (combinations.TryGetValue(combinationChoice, out MoveAttackCombo finalCombo))
            {
                Tile[] path = turnManager.pathfinder.PathBetween(finalCombo.movementTile, enemy_base.characterTile);
                enemy_base.MoveAndAttack(path, null, turnManager, false, Vector3.zero);

                //Waits for the enemy to finish its movement
                while (enemy_base.moving)
                {
                    yield return new WaitForSeconds(0.1f);
                }

                //Checks if it should perform an Attack action
                if (finalCombo.attackRotation != nullVector)
                {
                    enemyAttackArea.transform.position = finalCombo.attackingTile.transform.position;
                    enemyAttackArea.transform.eulerAngles = finalCombo.attackRotation;

                    //IMPORTANT YIELD which lets the triggers update
                    yield return new WaitForSeconds(0.02f);
                    enemyAttackArea.DetectArea(true, false);

                    foreach (Character character in enemyAttackArea.CharactersHit(TurnEnums.CharacterType.Player))
                    {
                        TemporaryMarker.GenerateMarker(enemy_base.enemySO.attributes.hitMarker, character.transform.position, 4f, 0.5f);
                    }

                    enemy_base.ExecuteAttack(enemyAttackArea, turnManager);
                }

                yield return new WaitForSeconds(0.5f / GameManager.Instance.GameSpeed);
                enemyAttackArea.DestroySelf();
                while (enemy_base.FollowUpEffect(enemyAttackArea, turnManager))
                {
                    yield return null;
                }

                yield return new WaitForSeconds(0.5f / GameManager.Instance.GameSpeed);
            }
            else
            {
                Debug.Log($"!!-- {enemy_base.name} FAILED TO FIND FINAL COMBINATION KEY --!!");
            }

            //Resets Variables
            combinations.Clear();
            lowestValue = 100;

            enemy_base.ActionCleanup();
        }

        turnManager.pathfinder.ResetPathFinder();
        uniqueIds = 0;
        DecisionMakingFinished = true;
    }

    //Determines what rotation should be applied to the AttackArea
    private Vector3 DetermineAttackAreaRotation(Tile targetTile, Tile movementTile)
    {
        Transform movementTransform = movementTile.transform;
        Transform tileTransform = targetTile.transform;

        float rotation = movementTransform.eulerAngles.y;

        float angle = Vector3.Angle(movementTransform.forward, (tileTransform.position - movementTransform.position));

        if (Vector3.Distance(tileTransform.position, movementTransform.position + (movementTransform.right * 6)) <
            Vector3.Distance(tileTransform.position, movementTransform.position + (-movementTransform.right) * 6))
        {
            rotation += angle;
        }
        else
        {
            rotation -= angle;
        }

        return new Vector3(0, rotation, 0);
    }

    //Adds a combination into the combination dictionary
    private void AddCombination(Tile moveTile, Tile attackTile, Vector3 attackRotation, int combinationValue)
    {
        combinations.Add(uniqueIds, new MoveAttackCombo(combinationValue, moveTile, attackTile, attackRotation));

        uniqueIds++;
        CalculateLowestValue();
    }

    //Removes a combination from the combination dictionary
    private void RemoveCombination(int key)
    {
        combinations.Remove(key);
    }

    //Determines what the lowest value in the combination dictionary is
    private void CalculateLowestValue()
    {
        lowestValue = 100;
        foreach (MoveAttackCombo combo in combinations.Values)
        {
            if (combo.value < lowestValue)
            {
                lowestValue = combo.value;
            }
        }
    }

    //Checks if we should be keeping a new Combo
    private void CheckIfComboKeep(Tile moveTile, Tile attackTile, Vector3 attackRotation, int combinationValue)
    {
        //Only attempts to replace another combination if one of the previous ones is worse or equal in value
        if (combinationValue >= lowestValue)
        {
            //Finds all the combinations of the lowest value
            foreach (KeyValuePair<int, MoveAttackCombo> comboValues in combinations)
            {
                if(attackRotation == nullVector && comboValues.Value.attackRotation != nullVector)
                {
                    continue;
                }

                if (comboValues.Value.value == lowestValue)
                {
                    inThreatKeys.Add(comboValues.Key);
                }
            }

            if (inThreatKeys.Count == 0)
            {
                return;
            }

            //Determines how the combination dispute should be settled
            if (combinationValue == lowestValue)
            {
                SettleValueConflict(moveTile, attackTile, attackRotation, combinationValue, true);
            }
            else if (inThreatKeys.Count == 1)
            {
                RemoveCombination(inThreatKeys[0]);
                AddCombination(moveTile, attackTile, attackRotation, combinationValue);
            }
            else
            {
                SettleValueConflict(moveTile, attackTile, attackRotation, combinationValue, false);
            }

            inThreatKeys.Clear();
        }
    }

    //Decides what combinations to keep when attempting to add a new one
    private void SettleValueConflict(Tile moveTile, Tile attackTile, Vector3 attackRotation, int combinationValue, bool newAtThreat)
    {
        int choice;
        if (newAtThreat)
        {
            choice = Random.Range(0, 5);
            if (choice > 1)
            {
                return;
            }
        }

        choice = Random.Range(0, inThreatKeys.Count);

        RemoveCombination(inThreatKeys[choice]);

        AddCombination(moveTile, attackTile, attackRotation, combinationValue);
    }

    //Decides what one of the final combinations will be executed
    private int PickCombination()
    {
        int totalValueOfCombinations = 0;
        int currentTotal = 0;
        int choice;

        //Totals up the values
        foreach (MoveAttackCombo combo in combinations.Values)
        {
            if (combo.value < 1)
            {
                combo.value = 1;
            }
            totalValueOfCombinations += combo.value;
        }

        choice = Random.Range(0, totalValueOfCombinations + 1);

        foreach (KeyValuePair<int, MoveAttackCombo> combo in combinations)
        {
            currentTotal += combo.Value.value;
            if (currentTotal >= choice)
            {
                choice = combo.Key;
                break;
            }
        }

        return choice;
    }

    //If there is a final combination that results in an Attack being performed all non attacking combinations are discarded
    private bool IfAttacksClearNon()
    {
        bool attackingComboFound = false;
        List<int> keysToRemove = new List<int>();

        foreach (KeyValuePair<int, MoveAttackCombo> combo in combinations)
        {
            if (combo.Value.attackRotation == nullVector)
            {
                keysToRemove.Add(combo.Key);
            }
            else
            {
                attackingComboFound = true;
            }
        }

        if (attackingComboFound)
        {
            foreach (int key in keysToRemove)
            {
                RemoveCombination(key);
            }
            return true;
        }
        else
        {
            return false;
        }
    }

    #endregion
}
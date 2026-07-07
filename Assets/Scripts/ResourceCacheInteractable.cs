using System;
using UnityEngine;

public sealed class ResourceCacheInteractable : MonoBehaviour, IGridInteractable
{
    private GameGrid grid;
    private Vector2Int cell;
    private string cacheId;
    private CraftMaterialId primaryMaterial;
    private CraftMaterialId qualityMaterial;
    private int minAmount;
    private int maxAmount;
    private float qualityChance;
    private SpriteRenderer spriteRenderer;

    public Vector3 InteractionPosition => grid != null ? grid.GridToWorld(cell.x, cell.y) : transform.position;

    public void Initialize(
        GameGrid gameGrid,
        Vector2Int position,
        string id,
        CraftMaterialId material,
        int minimumAmount,
        int maximumAmount,
        float qualityMaterialChance,
        Sprite sprite,
        Color tint)
    {
        grid = gameGrid;
        cell = position;
        cacheId = id;
        primaryMaterial = material;
        qualityMaterial = QualityVariant(material);
        minAmount = Mathf.Max(1, minimumAmount);
        maxAmount = Mathf.Max(minAmount, maximumAmount);
        qualityChance = Mathf.Clamp01(qualityMaterialChance);

        transform.position = grid.GridToWorld(cell.x, cell.y);
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = tint;
        spriteRenderer.sortingOrder = SortingLayers.Entity(transform.position.y);

        float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        transform.localScale = Vector3.one * grid.CellSize * 0.55f / Mathf.Max(0.0001f, spriteSize);
    }

    public void Interact(Player player)
    {
        if (RunState.IsResourceCacheLooted(cacheId))
        {
            DialogueUI.Instance.Show("Здесь уже ничего полезного нет.", 1.6f);
            return;
        }

        var rng = new System.Random(StableSeed(cacheId, RunState.Day));
        int amount = rng.Next(minAmount, maxAmount + 1);
        bool foundQuality = rng.NextDouble() < qualityChance;

        RunState.AddCraftMaterial(primaryMaterial, amount);
        string message = $"Найдено: {RunState.MaterialName(primaryMaterial)} x{amount}";

        if (foundQuality && qualityMaterial != primaryMaterial)
        {
            RunState.AddCraftMaterial(qualityMaterial, 1);
            message += $", {RunState.MaterialName(qualityMaterial)} x1";
        }

        RunState.MarkResourceCacheLooted(cacheId);
        if (spriteRenderer != null) spriteRenderer.color *= new Color(0.45f, 0.45f, 0.45f, 0.85f);
        DialogueUI.Instance.Show(message, 2.2f);
    }

    private static int StableSeed(string id, int day)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in id)
            {
                hash = hash * 31 + c;
            }
            return hash ^ (day * 7919);
        }
    }

    private static CraftMaterialId QualityVariant(CraftMaterialId material) => material switch
    {
        CraftMaterialId.Chemicals => CraftMaterialId.QualityChemicals,
        CraftMaterialId.ScrapMetal => CraftMaterialId.QualityScrapMetal,
        CraftMaterialId.Microchips => CraftMaterialId.QualityMicrochips,
        _ => material,
    };
}

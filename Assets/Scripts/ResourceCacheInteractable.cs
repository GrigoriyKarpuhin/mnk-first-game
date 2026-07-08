using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ResourceCacheInteractable : MonoBehaviour, IGridInteractable
{
    private GameGrid grid;
    private Vector2Int cell;
    private string cacheId;
    private int minAmount;
    private int maxAmount;
    private SpriteRenderer spriteRenderer;

    private const float SecondaryMaterialChance = 0.35f;
    private const float QualityMaterialChance = 0.16f;

    public Vector3 InteractionPosition => grid != null ? grid.GridToWorld(cell.x, cell.y) : transform.position;

    public void Initialize(
        GameGrid gameGrid,
        Vector2Int position,
        string id,
        int minimumAmount,
        int maximumAmount,
        Sprite sprite,
        Color tint)
    {
        grid = gameGrid;
        cell = position;
        cacheId = id;
        minAmount = Mathf.Max(1, minimumAmount);
        maxAmount = Mathf.Max(minAmount, maximumAmount);

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
        CraftMaterialId primaryMaterial = PickCommonMaterial(rng);
        int amount = rng.Next(minAmount, maxAmount + 1);

        RunState.AddCraftMaterial(primaryMaterial, amount);
        var parts = new List<string> { $"{RunState.MaterialName(primaryMaterial)} x{amount}" };

        if (rng.NextDouble() < SecondaryMaterialChance)
        {
            CraftMaterialId secondaryMaterial = PickDifferentCommonMaterial(rng, primaryMaterial);
            int secondaryAmount = rng.Next(1, 3);
            RunState.AddCraftMaterial(secondaryMaterial, secondaryAmount);
            parts.Add($"{RunState.MaterialName(secondaryMaterial)} x{secondaryAmount}");
        }

        if (rng.NextDouble() < QualityMaterialChance)
        {
            CraftMaterialId qualityMaterial = QualityVariant(primaryMaterial);
            RunState.AddCraftMaterial(qualityMaterial, 1);
            parts.Add($"{RunState.MaterialName(qualityMaterial)} x1");
        }

        RunState.MarkResourceCacheLooted(cacheId);
        if (spriteRenderer != null) spriteRenderer.color *= new Color(0.45f, 0.45f, 0.45f, 0.85f);
        DialogueUI.Instance.Show("Найдено: " + string.Join(", ", parts), 2.4f);
    }

    private static CraftMaterialId PickCommonMaterial(System.Random rng)
    {
        double roll = rng.NextDouble();
        if (roll < 0.42) return CraftMaterialId.ScrapMetal;
        if (roll < 0.78) return CraftMaterialId.Microchips;
        return CraftMaterialId.Chemicals;
    }

    private static CraftMaterialId PickDifferentCommonMaterial(System.Random rng, CraftMaterialId excluded)
    {
        for (int i = 0; i < 6; i++)
        {
            CraftMaterialId material = PickCommonMaterial(rng);
            if (material != excluded) return material;
        }

        return excluded == CraftMaterialId.ScrapMetal
            ? CraftMaterialId.Microchips
            : CraftMaterialId.ScrapMetal;
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

using System;
using System.Collections;
using System.Collections.Generic;
using LazyDuchess.MasterController;
using LazyDuchess.SmoothPatch;
using MonoPatcherLib;
using Sims3.SimIFace;
using Sims3.SimIFace.CAS;
using Sims3.UI;
using Sims3.UI.CAS;

namespace Arro.MCRSP
{
    public class SPPatch
    {
        [ReplaceMethod(typeof(LazyDuchess.SmoothPatch.ClothingPerformance), "AddGridItem")]
        private static bool AddGridItem(ItemGrid grid, object current, ResourceKey layoutKey, object context)
        {
            CASClothingCategory gSingleton = CASClothingCategory.gSingleton;
            bool result = false;

            if (current != null)
            {
                int totalItemsSoFar = grid.Count;
                int visibleColumns = (int)grid.VisibleColumns;

                int targetRow = totalItemsSoFar / visibleColumns;
                int targetCol = totalItemsSoFar % visibleColumns;

                return SetGridItemAtPosition(grid, current, layoutKey, context, targetRow, targetCol);
            }
            else
            {
                gSingleton.mContentTypeFilter.UpdateFilterButtonState();
                gSingleton.UpdateButtons(gSingleton.mSelectedType);
                if (CASClothingCategory.OnClothingGridFinishedPopulating != null)
                {
                    CASClothingCategory.OnClothingGridFinishedPopulating();
                }
            }

            return result;
        }

        [ReplaceMethod(typeof(LazyDuchess.SmoothPatch.ClothingPerformance), "HookedPopulateTypesGrid")]
        private static void HookedPopulateTypesGrid()
        {
            LoadedItems.Clear();
            CASClothingCategory gSingleton = CASClothingCategory.gSingleton;
            if (gSingleton == null)
            {
                return;
            }

            ItemGrid mClothingTypesGrid = gSingleton.mClothingTypesGrid;
            if (mClothingTypesGrid == null)
            {
                return;
            }

            mClothingTypesGrid.Tick -= ClothingPerformance.ItemGrid_Tick;

            int visibleColumns = (int)mClothingTypesGrid.VisibleColumns;
            int visibleRows = (int)mClothingTypesGrid.VisibleRows;

            mClothingTypesGrid.mPopulateStride = 0;
            mClothingTypesGrid.AbortPopulating();
            mClothingTypesGrid.Clear();

            if (gSingleton.mCategoryText.Caption.Equals(gSingleton.GetClothingStateName(CASClothingState.Career)))
            {
                ICASModel casmodel = Responder.Instance.CASModel;
                if (casmodel == null)
                {
                    return;
                }

                if (casmodel.OutfitIndex == 0)
                {
                    if (CASClothingCategory.OnClothingGridFinishedPopulating != null)
                    {
                        CASClothingCategory.OnClothingGridFinishedPopulating();
                    }

                    if (!CASController.Singleton.AccessCareer && !Responder.Instance.CASModel.IsEditingUniform)
                    {
                        return;
                    }
                }
            }

            gSingleton.mCurrentPreset = null;
            gSingleton.mCurrentFocusedRow = null;
            gSingleton.mTempFocusedRow = null;
            gSingleton.mSelectedType = CASClothingRow.SelectedTypes.None;
            gSingleton.mShareButton.Enabled = false;
            gSingleton.mTrashButton.Enabled = false;
            gSingleton.mSaveButton.Enabled = false;
            gSingleton.mSortButton.Enabled = true;
            gSingleton.mSortButton.Tag = false;
            mClothingTypesGrid.mPopulateCallback = null;

            ClothingPerformance.partList.Clear();
            ClothingPerformance.CacheWornParts();

            foreach (object obj in gSingleton.mPartsList)
            {
                if (obj != null && CASPuck.GetContentTypeFilter().ObjectMatchesFilter(obj))
                {
                    ClothingPerformance.partList.Add(obj);
                }
            }

            int itemsPerPage = visibleColumns * visibleRows;

            int loadedCount = 0;
            int currentItemIndex = 0;
            mClothingTypesGrid.mPopulating = true;

            while (loadedCount < itemsPerPage && currentItemIndex < ClothingPerformance.partList.Count)
            {
                object obj = ClothingPerformance.partList[currentItemIndex];
                if (obj == null)
                {
                    break;
                }

                int row = loadedCount / visibleColumns;
                int col = loadedCount % visibleColumns;

                if (SetGridItemAtPosition(mClothingTypesGrid, obj, ClothingPerformance.layoutKey,
                        null, row, col))
                {
                    LoadedItems[$"{row}_{col}"] = currentItemIndex;
                    loadedCount++;
                }

                currentItemIndex++;
            }
            
            if (loadedCount > 0)
            {
                int lastRow = (loadedCount - 1) / visibleColumns;
                int lastCol = (loadedCount - 1) % visibleColumns;
                mClothingTypesGrid.mLastEntryI = lastCol;
                mClothingTypesGrid.mLastEntryJ = lastRow;
            }
            else
            {
                mClothingTypesGrid.mLastEntryI = -1;
                mClothingTypesGrid.mLastEntryJ = 0;
            }

            while (loadedCount < itemsPerPage)
            {
                int row = loadedCount / visibleColumns;
                int col = loadedCount % visibleColumns;
                LoadedItems[$"{row}_{col}"] = -1;
                loadedCount++;
            }

            int totalItems = ClothingPerformance.partList.Count;
            int totalRows = (int)Math.Ceiling(totalItems / (double)visibleColumns);

            for (int row = visibleRows; row < totalRows; row++)
            {
                for (int col = 0; col < visibleColumns; col++)
                {
                    if (row == totalRows - 1 && col == visibleColumns - 1)
                    {
                        mClothingTypesGrid.mPopulating = false;
                    }

                    ClothingPerformance.AddItem(mClothingTypesGrid,
                        new ItemGridCellItem(ClothingPerformance.placeHolderRow, null));
                }
            }

            mClothingTypesGrid.mPopulating = false;

            mClothingTypesGrid.Tick += ClothingPerformance.ItemGrid_Tick;

            gSingleton.mContentTypeFilter.UpdateFilterButtonState();
            gSingleton.UpdateButtons(gSingleton.mSelectedType);

            if (CASClothingCategory.OnClothingGridFinishedPopulating != null)
            {
                CASClothingCategory.OnClothingGridFinishedPopulating();
            }
        }

        [ReplaceMethod(typeof(ClothingPerformance), "ItemGrid_Tick")]
        private static void ItemGrid_Tick(WindowBase sender, UIEventArgs eventArgs)
        {
            ItemGrid mClothingTypesGrid = CASClothingCategory.gSingleton.mClothingTypesGrid;

            int visibleColumns = (int)mClothingTypesGrid.VisibleColumns;
            int visibleRows = (int)mClothingTypesGrid.VisibleRows;

            double scrollPosition = (double)mClothingTypesGrid.VScrollbar.Value / 135.0; // Row height
            int firstVisibleRow = (int)Math.Floor(scrollPosition);
            int lastVisibleRow = firstVisibleRow + visibleRows;

            for (int row = firstVisibleRow; row <= lastVisibleRow; row++)
            {
                for (int col = 0; col < visibleColumns; col++)
                {
                    string key = $"{row}_{col}";

                    if (!LoadedItems.ContainsKey(key) ||
                        LoadedItems[key] == -1)
                    {
                        int itemIndex = (row * visibleColumns) + col;

                        if (itemIndex < ClothingPerformance.partList.Count)
                        {
                            object obj = ClothingPerformance.partList[itemIndex];
                            if (obj != null)
                            {
                                if (SetGridItemAtPosition(
                                        mClothingTypesGrid, obj, ClothingPerformance.layoutKey,
                                        null, row, col))
                                {
                                    LoadedItems[key] = itemIndex;
                                    return;
                                }
                            }
                        }
                        else
                        {
                            LoadedItems[key] = -2;
                        }
                    }
                }
            }
        }

        private static bool SetGridItemAtPosition(ItemGrid grid, object current, ResourceKey layoutKey, object context,
            int targetRow, int targetCol)
        {
            CASClothingCategory gSingleton = CASClothingCategory.gSingleton;
            bool result = false;

            if (current != null)
            {
                if (current is CASPart)
                {
                    CASPart caspart = (CASPart)current;
                    CASClothingRow casclothingRow =
                        UIManager.LoadLayout(layoutKey).GetWindowByExportID(1) as CASClothingRow;
                    casclothingRow.UseEp5AsBaseContent = gSingleton.mIsEp5Base;
                    casclothingRow.CASPart = caspart;
                    casclothingRow.RowController = gSingleton;
                    ArrayList arrayList = ClothingPerformance.CreateGridItems(casclothingRow, true);
                    gSingleton.mSortButton.Tag =
                        ((bool)gSingleton.mSortButton.Tag | casclothingRow.HasFilterableContent);

                    if (arrayList.Count > 0)
                    {
                        if (targetRow >= grid.EntriesCountJ)
                        {
                            grid.EntriesCountJ = targetRow + 1;
                            grid.mGrid.SetRowHeight(targetRow, grid.mGrid.DefaultRowHeight);
                        }

                        grid.InternalGrid.SetCellWindow(targetCol, targetRow, casclothingRow,
                            grid.mbStretchCellWindows);
                        grid.InternalGrid.CellTags[targetCol, targetRow] = casclothingRow;
                        result = true;
                        
                        grid.mLastEntryI = targetCol;
                        grid.mLastEntryJ = targetRow;

                        if (casclothingRow.SelectedItem != -1)
                        {
                            if (gSingleton.IsAccessoryType(caspart.BodyType))
                            {
                                if (gSingleton.GetWornPart((BodyTypes)CASClothingCategory.sAccessoriesSelection).Key !=
                                    ResourceKey.kInvalidResourceKey)
                                {
                                    if (caspart.BodyType == (BodyTypes)CASClothingCategory.sAccessoriesSelection)
                                    {
                                        int gridIndex = (targetRow * (int)grid.VisibleColumns) + targetCol;
                                        grid.SelectedItem = gridIndex;
                                        gSingleton.mSelectedType = casclothingRow.SelectedType;
                                        CASClothingCategory.sAccessoriesSelection = (int)caspart.BodyType;
                                        gSingleton.mCurrentPreset = (casclothingRow.Selection as CASPartPreset);
                                    }
                                }
                                else
                                {
                                    int gridIndex = (targetRow * (int)grid.VisibleColumns) + targetCol;
                                    grid.SelectedItem = gridIndex;
                                    gSingleton.mSelectedType = casclothingRow.SelectedType;
                                    CASClothingCategory.sAccessoriesSelection = (int)caspart.BodyType;
                                    gSingleton.mCurrentPreset = (casclothingRow.Selection as CASPartPreset);
                                }
                            }
                            else
                            {
                                int gridIndex = (targetRow * (int)grid.VisibleColumns) + targetCol;
                                grid.SelectedItem = gridIndex;
                                gSingleton.mSelectedType = casclothingRow.SelectedType;
                                gSingleton.mCurrentPreset = (casclothingRow.Selection as CASPartPreset);
                            }
                        }
                    }

                    if (LazyDuchess.MasterController.MasterController.Active)
                    {
                        CASClothingRowEx.Create(casclothingRow, true);
                    }
                }
                else
                {
                    List<object> list = current as List<object>;
                    if (list != null)
                    {
                        CASClothingRow casclothingRow =
                            UIManager.LoadLayout(layoutKey).GetWindowByExportID(1) as CASClothingRow;
                        casclothingRow.ObjectOfInterest = list;
                        casclothingRow.RowController = gSingleton;
                        ArrayList arrayList = ClothingPerformance.CreateGridItems(casclothingRow, true);
                        gSingleton.mSortButton.Tag =
                            ((bool)gSingleton.mSortButton.Tag | casclothingRow.HasFilterableContent);

                        if (arrayList.Count > 0)
                        {
                            if (targetRow >= grid.EntriesCountJ)
                            {
                                grid.EntriesCountJ = targetRow + 1;
                                grid.mGrid.SetRowHeight(targetRow, grid.mGrid.DefaultRowHeight);
                            }

                            grid.InternalGrid.SetCellWindow(targetCol, targetRow, casclothingRow,
                                grid.mbStretchCellWindows);
                            grid.InternalGrid.CellTags[targetCol, targetRow] = casclothingRow;
                            
                            grid.mLastEntryI = targetCol;
                            grid.mLastEntryJ = targetRow;
                            
                            result = true;
                        }
                    }
                }
            }
            
            return result;
        }
        private static Dictionary<string, int> LoadedItems = new Dictionary<string, int>();
    }
}
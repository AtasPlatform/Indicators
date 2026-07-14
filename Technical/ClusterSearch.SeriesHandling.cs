namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;

using OFT.Localization;

public partial class ClusterSearch
{
	#region Private fields

	private static readonly string _tipPrefix = "Cluster Search" + Environment.NewLine;

	private CalcMode _tipSuffixFor = (CalcMode)(-1);
	private string _tipSuffix = "";

	#endregion

	#region Private methods

	//Binary search of insert index to keep price values sorted
	private void InsertOrReplace(int bar, PriceSelectionValue value)
	{
		var index = GetSeriesLevelIndex(bar, value.MinimumPrice);

		if (index >= 0)
			_lastSeriesBar[index] = value;
		else
			_lastSeriesBar.Insert(~index, value);
	}

	private void RemoveOldSelection(int bar, decimal price)
	{
		var idx = GetSeriesLevelIndex(bar, price);

		if (idx >= 0)
			_lastSeriesBar.RemoveAt(idx);
	}

	//Insert or replace price level in series
	private void PlaceToDataSeries(int bar, CustomVolumeInfo cluster)
	{
		var value = CalcType switch
		{
			CalcMode.Bid => cluster.Bid,
			CalcMode.Ask => cluster.Ask,
			CalcMode.Delta => cluster.Delta,
			CalcMode.Volume or CalcMode.MaxVolume => cluster.Volume,
			CalcMode.Tick => cluster.Ticks,
			_ => 0
		};

		PriceSelectionValue level = null;

		if (OnlyOneSelectionPerBar
		    && CalcType is not CalcMode.MaxVolume
		    && _lastSeriesBar.Count is not 0)
		{
			if (_lastSeriesBar[0].Context is decimal vol)
			{
				var newMax = CalcType is CalcMode.Delta
					? Math.Abs(vol) < Math.Abs(value)
					: vol < value;

				if (newMax)
				{
					level = CreatePriceSelectionValue(cluster);
					_lastSeriesBar[0] = level;
				}
			}
		}
		else
		{
			level = CreatePriceSelectionValue(cluster);
			InsertOrReplace(bar, level);
		}

		if (UseAlerts && _alertPrices.Add(cluster.Price) && _isFinishRecalculate)
			AddClusterAlert(level?.Tooltip ?? CreateToolTip(value));
	}

	//Find index of price level by price
	private int GetSeriesLevelIndex(int bar, decimal value)
	{
		return _lastSeriesBar.SyncGet(GetSeriesLevelIndex, value);
	}

	private static int GetSeriesLevelIndex(List<PriceSelectionValue> list, decimal value)
	{
		int left = 0, right = list.Count;

		while (left < right)
		{
			var mid = left + (right - left) / 2;

			if (list[mid].MinimumPrice < value)
				left = mid + 1;
			else if (list[mid].MinimumPrice > value)
				right = mid;
			else
				return mid;
		}

		return ~left;
	}

	//Create level value for data series
	private PriceSelectionValue CreatePriceSelectionValue(CustomVolumeInfo cluster)
	{
		var selectionSide = CalcType switch
		{
			CalcMode.Ask => SelectionType.Ask,
			CalcMode.Bid => SelectionType.Bid,
			_ => SelectionType.Full
		};

		var value = CalcType switch
		{
			CalcMode.Bid => cluster.Bid,
			CalcMode.Ask => cluster.Ask,
			CalcMode.Delta => cluster.Delta,
			CalcMode.Volume or CalcMode.MaxVolume => cluster.Volume,
			CalcMode.Tick => cluster.Ticks,
			_ => 0
		};

		var absValue = CalcType is CalcMode.Delta 
			? Math.Abs(value) 
			: value;

		var clusterSize = GetClusterSize(absValue);

		var priceValue = new PriceSelectionValue(cluster.Price)
		{
			VisualObject = VisualType,
			Size = clusterSize,
			SelectionSide = selectionSide,
			ObjectColor = _clusterTransColor,
			ObjectsTransparency = _visualObjectsTransparency,
			PriceSelectionColor = ShowPriceSelection ? _clusterPriceColor : CrossColors.Transparent,
			Tooltip = CreateToolTip(value),
			Context = absValue,
			MinimumPrice = cluster.Price,
			MaximumPrice = cluster.Price + InstrumentInfo.TickSize * (PriceRange - 1)
		};

		return priceValue;
	}

	//Create tooltip text for PriceSelectionValue
	private string CreateToolTip(decimal value)
	{
		if (_tipSuffixFor != CalcType)
		{
			_tipSuffix = " " + CalcType switch
			{
				CalcMode.Bid => Strings.Bid,
				CalcMode.Ask => Strings.Ask,
				CalcMode.Delta => Strings.Delta,
				CalcMode.Volume => Strings.Volume,
				CalcMode.Tick => Strings.Ticks,
				CalcMode.MaxVolume => Strings.PocLevel,
				_ => ""
			};
			_tipSuffixFor = CalcType;
		}

		return _tipPrefix + ChartInfo.TryGetMinimizedVolumeString(value) + _tipSuffix;
	}

	#endregion
}
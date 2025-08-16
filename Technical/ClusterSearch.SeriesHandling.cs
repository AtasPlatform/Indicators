namespace ATAS.Indicators.Technical;

using System;

using OFT.Localization;

public partial class ClusterSearch
{
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

	//Check ranges between previous and current trade prices when Price Location filter is not Any
	private void UpdatePriceLocationValues(int bar, MarketDataArg trade)
	{
		var candle = GetCandle(bar);

		switch (PriceLoc)
		{
			case PriceLocation.AtHigh:
				if (_lastSeriesBar.Count is 0)
					return;

				if (trade.Price != candle.High)
					return;

				RemoveOldSelection(bar, _lastPrice, trade.Price - InstrumentInfo.TickSize);
				break;

			case PriceLocation.AtLow:
				if (_lastSeriesBar.Count is 0)
					return;

				if (trade.Price != candle.Low)
					return;

				RemoveOldSelection(bar, trade.Price + InstrumentInfo.TickSize, _lastPrice);
				break;

			case PriceLocation.Body:
			{
				if (trade.Price >= candle.Open)
				{
					if (_lastPrice > candle.Open)
					{
						if (_lastPrice > trade.Price)
							RemoveOldSelection(bar, trade.Price + InstrumentInfo.TickSize, _lastPrice);
						else
							CheckPriceRange(bar, _lastPrice, trade.Price - InstrumentInfo.TickSize);
					}
					else
					{
						RemoveOldSelection(bar, _lastPrice, candle.Open);
						CheckPriceRange(bar, candle.Open, trade.Price - InstrumentInfo.TickSize);
					}
				}
				else
				{
					if (_lastPrice > candle.Open)
					{
						RemoveOldSelection(bar, _lastPrice, candle.Open);
						CheckPriceRange(bar, candle.Open, trade.Price + InstrumentInfo.TickSize);
					}
					else
					{
						if (_lastPrice > trade.Price)
							CheckPriceRange(bar, _lastPrice, trade.Price + InstrumentInfo.TickSize);
						else
							RemoveOldSelection(bar, trade.Price - InstrumentInfo.TickSize, _lastPrice);
					}
				}

				break;
			}
			case PriceLocation.UpperWick:
			{
				if (trade.Price >= candle.Open)
				{
					if (_lastPrice > candle.Open)
					{
						if (_lastPrice > trade.Price)
							CheckPriceRange(bar, trade.Price + InstrumentInfo.TickSize, _lastPrice);
						else
							RemoveOldSelection(bar, _lastPrice, trade.Price);
					}
					else
						RemoveOldSelection(bar, candle.Open, trade.Price);
				}
				else
				{
					if (_lastPrice > candle.Open)
					{
						RemoveOldSelection(bar, trade.Price, candle.Open);
						CheckPriceRange(bar, candle.Open + InstrumentInfo.TickSize, _lastPrice);
					}
				}

				break;
			}
			case PriceLocation.LowerWick:
			{
				if (trade.Price >= candle.Open)
				{
					if (_lastPrice < candle.Open)
					{
						RemoveOldSelection(bar, candle.Open, trade.Price);
						CheckPriceRange(bar, _lastPrice, candle.Open - InstrumentInfo.TickSize);
					}
				}
				else
				{
					if (_lastPrice > candle.Open)
						RemoveOldSelection(bar, trade.Price, _lastPrice);
					else
					{
						if (_lastPrice > trade.Price)
							RemoveOldSelection(bar, trade.Price, _lastPrice);
						else
							CheckPriceRange(bar, _lastPrice, trade.Price - InstrumentInfo.TickSize);
					}
				}

				break;
			}
			case PriceLocation.AtHighOrLow:
			{
				if (_lastSeriesBar.Count is 0)
					return;

				if (trade.Price == candle.High && _lastPrice != candle.Low)
					RemoveOldSelection(bar, _lastPrice, trade.Price - InstrumentInfo.TickSize);

				else if (trade.Price == candle.Low && _lastPrice != candle.High)
					RemoveOldSelection(bar, trade.Price + InstrumentInfo.TickSize, _lastPrice);
				break;
			}
			case PriceLocation.AtUpperLowerWick:
				if (trade.Price >= candle.Open)
				{
					if (_lastPrice < candle.Open)
					{
						CheckPriceRange(bar, _lastPrice, candle.Open - InstrumentInfo.TickSize);
						RemoveOldSelection(bar, candle.Open, trade.Price);
					}
					else
					{
						if (_lastPrice > trade.Price)
							CheckPriceRange(bar, trade.Price + InstrumentInfo.TickSize, _lastPrice);
						else
							RemoveOldSelection(bar, _lastPrice, trade.Price);
					}
				}
				else
				{
					if (_lastPrice > candle.Open)
					{
						if (_lastPrice > trade.Price)
						{
							CheckPriceRange(bar, candle.Open + InstrumentInfo.TickSize, _lastPrice);
							RemoveOldSelection(bar, trade.Price, candle.Open);
						}
					}
					else
					{
						if (_lastPrice > trade.Price)
							RemoveOldSelection(bar, trade.Price, _lastPrice);
						else
							CheckPriceRange(bar, _lastPrice, trade.Price - InstrumentInfo.TickSize);
					}
				}

				break;
		}
	}

	//Remove price selection from series by bar and price
	private void RemoveOldSelection(int bar, decimal price)
	{
		var idx = GetSeriesLevelIndex(bar, price);

		if (idx >= 0)
			_lastSeriesBar.RemoveAt(idx);
	}

	private void RemoveOldSelection(int bar, decimal from, decimal to)
	{
		for (var price = from; price <= to; price += InstrumentInfo.TickSize)
			RemoveOldSelection(bar, price);
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

		var level = CreatePriceSelectionValue(cluster);

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
					_lastSeriesBar[0] = level;
			}
		}
		else
		{
			InsertOrReplace(bar, level);
		}

		if (UseAlerts && _alertPrices.Add(cluster.Price) && _isFinishRecalculate)
			AddClusterAlert(level.Tooltip);
	}

	//Find index of price level by price
	private int GetSeriesLevelIndex(int bar, decimal value)
	{
		int left = 0, right = _lastSeriesBar.Count;

		while (left < right)
		{
			var mid = left + (right - left) / 2;

			if (_lastSeriesBar[mid].MinimumPrice < value)
				left = mid + 1;
			else if (_lastSeriesBar[mid].MinimumPrice > value)
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

		var clusterWeight = absValue * _size / Math.Max(_minFilterValue, 1);

        var clusterSize = FixedSizes 
			? _size 
			: clusterWeight switch
			{
				> int.MaxValue => int.MaxValue,
                < int.MinValue => int.MinValue,
                _ => (int)clusterWeight
			};

		if (!FixedSizes)
		{
			clusterSize = Math.Min(clusterSize, MaxSize);
			clusterSize = Math.Max(clusterSize, MinSize);
		}

		var priceValue = new PriceSelectionValue(cluster.Price)
		{
			VisualObject = VisualType,
			Size = clusterSize,
			SelectionSide = selectionSide,
			ObjectColor = _clusterTransColor,
			ObjectsTransparency = _visualObjectsTransparency,
            PriceSelectionColor = cluster.PriceSelectionColor ?? (ShowPriceSelection ? _clusterPriceColor : CrossColors.Transparent),
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
		var tip = "Cluster Search" + Environment.NewLine + ChartInfo.TryGetMinimizedVolumeString(value) + " ";

		tip += CalcType switch
		{
			CalcMode.Bid => Strings.Bid,
			CalcMode.Ask => Strings.Ask,
			CalcMode.Delta => Strings.Delta,
			CalcMode.Volume => Strings.Volume,
			CalcMode.Tick => Strings.Ticks,
			CalcMode.MaxVolume => Strings.PocLevel,
			_ => ""
		};

		return tip;
	}

	#endregion
}
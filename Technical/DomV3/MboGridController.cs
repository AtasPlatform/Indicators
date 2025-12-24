namespace DomV10;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using ATAS.DataFeedsCore;
using ATAS.Indicators;

using Utils.Common.Collections;

using MarketDataType = ATAS.DataFeedsCore.MarketDataType;

public class OrderInfo
{
	#region Fields

	public bool IsNeedToBeRemove;
	public MarketByOrder Order;
	public MarketDataType Type;

	#endregion

	#region ctor

	public OrderInfo(MarketByOrder order)
	{
		Order = order;
		Type = order.Side;
	}

	#endregion

	#region Public methods

	public void Update(MarketByOrder order)
	{
		Order = order;
		Type = order.Side;
	}

	public void RemoveFlag(bool state)
	{
		IsNeedToBeRemove = state;
	}

	public decimal MaxVol()
	{
		return Order.Volume;
	}

	#endregion
}

public class RowItem
{
	#region Fields

	private readonly ConcurrentDictionary<long, OrderInfo> _orders = new();
	private readonly List<OrderInfo> _orderedBuffer = [];
	private OrderInfo[] _orderedCache = [];
	private bool _isCacheDirty = true;

	#endregion

	#region Properties

	public MarketDataType Type { get; private set; } = MarketDataType.Trade;

	#endregion

	#region Public methods

	public (decimal vol, int count) UpdateOrder(MarketByOrder order)
	{
		if (order.Side is MarketDataType.Bid or MarketDataType.Ask)
		{
			var clearFlag = false;

			if (order.Side != Type)
			{
				if (order.Type is MarketByOrderUpdateTypes.New or MarketByOrderUpdateTypes.Snapshot)
				{
					clearFlag = true;
				}
				else if (order.Type is MarketByOrderUpdateTypes.Change)
				{
					if (order.Volume != 0)
						clearFlag = true;
				}
			}

			if (clearFlag)
			{
				RemoveAllAgain(order.Side);
				Type = order.Side;
			}

			if (order.Type is MarketByOrderUpdateTypes.Delete ||
			    (order.Type is MarketByOrderUpdateTypes.Change && order.Volume == 0))
				SetRemoveFlag(order.ExchangeOrderId);
			else
			{
				if (!_orders.TryGetValue(order.ExchangeOrderId, out var orderInfo))
				{
					orderInfo = new OrderInfo(order);
					_orders[order.ExchangeOrderId] = orderInfo;
					InvalidateCache();
				}
				else
				{
					var previousPriority = orderInfo.Order?.Priority;
					orderInfo.Update(order);

					if (previousPriority != orderInfo.Order?.Priority)
						InvalidateCache();
				}
            }
        }

		RemoveExpireOrder();

		var vol = _orders.Sum(e => e.Value.MaxVol());
		var count = _orders.Count;
		return (count > 0 ? vol : 0, count);
	}

	public void UpdateTrade(MarketDataArg trade)
	{
	}

	public OrderInfo[] GetOrderedData()
	{
		if (_isCacheDirty)
			RebuildCache();

		return _orderedCache;
	}

	public void RemoveExpireOrder()
	{
		if (_orders.IsEmpty)
			return;

		var removed = false;

		foreach (var kvp in _orders)
		{
			if (kvp.Value.IsNeedToBeRemove && _orders.TryRemove(kvp.Key, out _))
				removed = true;
		}

		if (removed)
			InvalidateCache();
	}

	#endregion

	#region Private methods

	private void RemoveAllAgain(MarketDataType type)
	{
		if (_orders.IsEmpty)
			return;

		var removed = false;

		foreach (var kvp in _orders)
		{
			if (kvp.Value.Type != type && _orders.TryRemove(kvp.Key, out _))
				removed = true;
		}

		if (removed)
			InvalidateCache();
	}

	private bool SetRemoveFlag(long orderExchangeOrderId)
	{
		if (_orders.TryGetValue(orderExchangeOrderId, out var order))
		{
			order.RemoveFlag(true);
			return true;
		}

		return false;
	}

	private void InvalidateCache()
	{
		_isCacheDirty = true;
	}

	private void RebuildCache()
	{
		if (_orders.IsEmpty)
		{
			_orderedBuffer.Clear();
			_orderedCache = [];
			_isCacheDirty = false;

			return;
		}

		_orderedBuffer.Clear();

		foreach (var entry in _orders)
			_orderedBuffer.Add(entry.Value);

		if (_orderedBuffer.Count > 1)
			_orderedBuffer.Sort(OrderInfoPriorityComparer.Instance);

		if (_orderedCache.Length != _orderedBuffer.Count)
			_orderedCache = new OrderInfo[_orderedBuffer.Count];

		_orderedBuffer.CopyTo(_orderedCache);

		_isCacheDirty = false;
	}

	private sealed class OrderInfoPriorityComparer : IComparer<OrderInfo>
	{
		public static readonly OrderInfoPriorityComparer Instance = new();

		public int Compare(OrderInfo? x, OrderInfo? y)
		{
			if (ReferenceEquals(x, y))
				return 0;

			if (x is null)
				return -1;

			if (y is null)
				return 1;

			var xPriority = x.Order?.Priority ?? 0;
			var yPriority = y.Order?.Priority ?? 0;

			return xPriority.CompareTo(yPriority);
		}
	}

	#endregion
}

public enum DataType
{
	Lvl2,
	Lvl3
}

public class MboGridController
{
    #region Fields

    private readonly Dictionary<long, MarketByOrder> _mboHistory = new Dictionary<long, MarketByOrder>();
    private readonly Dictionary<decimal, RowItem> _grid = new();
	private readonly object _level2UpdateLock = new();
	private readonly Dictionary<decimal, (decimal vol, int count)> _priceVolume = new();
	private readonly object _updateLock = new();
	private readonly Dictionary<decimal, MarketDataArg> _level2Data = new();
	private readonly Security _security = new();

	// Price range cache for MBO data
	private decimal _gridMinPrice;
	private decimal _gridMaxPrice;
	private bool _isGridPriceRangeDirty = true;

	// Price range cache for L2 data
	private decimal _level2MinPrice;
	private decimal _level2MaxPrice;
	private bool _isLevel2PriceRangeDirty = true;

	// Buffer for GetPricesInRange - reused to avoid allocations
	private readonly List<decimal> _pricesBuffer = [];
	private decimal[] _pricesResultCache = [];

	#endregion

	#region ctor

	#endregion

	#region Public methods

	public bool Update(IEnumerable<MarketByOrder> orders)
	{
		lock (_updateLock)
		{
			if (_grid.Count == 0)
				return false;

			UpdateList(orders);
		}

		return true;
	}

	public void Load(IEnumerable<MarketByOrder> marketByOrders)
	{
		lock (_updateLock)
		{
			Reset();
			UpdateList(marketByOrders);
		}
	}

	public void UpdateTrade(MarketDataArg trade)
	{
		lock (_updateLock)
		{
			if (trade.ExchangeOrderId == null || trade.AggressorExchangeOrderId == null)
				return;

			if (trade?.ExchangeOrderId <= 0 || trade?.AggressorExchangeOrderId <= 0)
				return;

			if (trade == null)
				return;

			if (_grid.Count == 0)
				return;

			if (_grid.TryGetValue(trade.Price, out var value))
				value?.UpdateTrade(trade);
		}
	}

	public void Tick()
	{
		lock (_updateLock)
		{
			if (_grid.Count == 0)
				return;

			foreach (var item in _grid)
				item.Value.RemoveExpireOrder();
		}
	}

	public (OrderInfo[] Orders, MarketDataType Type) GetItemInRow(decimal price, MarketDataArg lastAsk, MarketDataArg lastBid, bool forceReturnLevel2)
	{
		var nullItem = (Array.Empty<OrderInfo>(), MarketDataType.Trade);

		lock (_updateLock)
		{
			if (!forceReturnLevel2 && _grid.Count != 0)
			{
				if (_grid.TryGetValue(price, out var value))
				{
					if (value.Type is MarketDataType.Ask && price < lastAsk.Price)
						return nullItem;

					if (value.Type is MarketDataType.Bid && price > lastBid.Price)
						return nullItem;

					return (value.GetOrderedData(), value.Type);
				}
			}
			else
			{
				lock (_level2Data)
				{
					if (_level2Data.Count != 0)
					{
						if (_level2Data.TryGetValue(price, out var value))
						{
							if (value.DataType is ATAS.Indicators.MarketDataType.Ask && price < lastAsk.Price)
								return nullItem;

							if (value.DataType is ATAS.Indicators.MarketDataType.Bid && price > lastBid.Price)
								return nullItem;

							var type = value.DataType is ATAS.Indicators.MarketDataType.Ask
								? MarketDataType.Ask
								: MarketDataType.Bid;

							if (value.Volume > 0)
							{
								var order = new OrderInfo(new MarketByOrder
								{
									Price = value.Price, 
									Volume = value.Volume,
									Type = MarketByOrderUpdateTypes.Snapshot,
									ExchangeOrderId = 0,
									Priority = 0, 
									Side = type,
									Security = _security, 
									Time = value.Time
								});

								return ([order], type);
							}
						}
					}
				}
			}
		}

		return nullItem;
	}

	/// <summary>
	/// Gets the price range where data exists (MBO or L2).
	/// Uses caching to optimize frequent calls during rendering.
	/// </summary>
	public (decimal MinPrice, decimal MaxPrice, bool HasData) GetPriceRange()
	{
		lock (_updateLock)
		{
			if (_grid.Count > 0)
			{
				if (_isGridPriceRangeDirty)
					RebuildGridPriceRangeCache();

				return (_gridMinPrice, _gridMaxPrice, true);
			}
		}

		lock (_level2UpdateLock)
		{
			if (_level2Data.Count > 0)
			{
				if (_isLevel2PriceRangeDirty)
					RebuildLevel2PriceRangeCache();

				return (_level2MinPrice, _level2MaxPrice, true);
			}
		}

		return (0, 0, false);
	}

	private void RebuildGridPriceRangeCache()
	{
		if (_grid.Count == 0)
		{
			_gridMinPrice = 0;
			_gridMaxPrice = 0;
			_isGridPriceRangeDirty = false;
			return;
		}

		var minPrice = decimal.MaxValue;
		var maxPrice = decimal.MinValue;

		foreach (var price in _grid.Keys)
		{
			if (price < minPrice)
				minPrice = price;

			if (price > maxPrice)
				maxPrice = price;
		}

		_gridMinPrice = minPrice;
		_gridMaxPrice = maxPrice;
		_isGridPriceRangeDirty = false;
	}

	private void RebuildLevel2PriceRangeCache()
	{
		if (_level2Data.Count == 0)
		{
			_level2MinPrice = 0;
			_level2MaxPrice = 0;
			_isLevel2PriceRangeDirty = false;
			return;
		}

		var minPrice = decimal.MaxValue;
		var maxPrice = decimal.MinValue;

		foreach (var price in _level2Data.Keys)
		{
			if (price < minPrice) minPrice = price;
			if (price > maxPrice) maxPrice = price;
		}

		_level2MinPrice = minPrice;
		_level2MaxPrice = maxPrice;
		_isLevel2PriceRangeDirty = false;
	}

	/// <summary>
	/// Gets prices with data in the specified range, sorted in descending order.
	/// Uses a reusable buffer to avoid allocations.
	/// IMPORTANT: The returned array is reused - do not store a reference to it between frames!
	/// </summary>
	public decimal[] GetPricesInRange(decimal fixHigh, decimal fixLow, MarketDataArg? lastAsk, MarketDataArg? lastBid)
	{
		_pricesBuffer.Clear();

		lock (_updateLock)
		{
			if (_grid.Count > 0)
			{
				foreach (var kvp in _grid)
				{
					var price = kvp.Key;
					if (price < fixLow || price > fixHigh)
						continue;

					// Filter invalid asks/bids
					if (lastAsk != null && kvp.Value.Type is MarketDataType.Ask && price < lastAsk.Price)
						continue;
					if (lastBid != null && kvp.Value.Type is MarketDataType.Bid && price > lastBid.Price)
						continue;

					_pricesBuffer.Add(price);
				}

				return BuildPricesResult();
			}
		}

		lock (_level2UpdateLock)
		{
			if (_level2Data.Count > 0)
			{
				foreach (var kvp in _level2Data)
				{
					var price = kvp.Key;
					if (price < fixLow || price > fixHigh)
						continue;

					// Filter invalid asks/bids
					if (lastAsk != null && kvp.Value.DataType is ATAS.Indicators.MarketDataType.Ask && price < lastAsk.Price)
						continue;
					if (lastBid != null && kvp.Value.DataType is ATAS.Indicators.MarketDataType.Bid && price > lastBid.Price)
						continue;

					if (kvp.Value.Volume > 0)
						_pricesBuffer.Add(price);
				}

				return BuildPricesResult();
			}
		}

		return [];
	}

	private decimal[] BuildPricesResult()
	{
		if (_pricesBuffer.Count == 0)
			return [];

		// Sort in descending order
		_pricesBuffer.Sort((a, b) => b.CompareTo(a));

		// Reuse array if size matches, otherwise create a new one
		if (_pricesResultCache.Length != _pricesBuffer.Count)
			_pricesResultCache = new decimal[_pricesBuffer.Count];

		_pricesBuffer.CopyTo(_pricesResultCache);
		return _pricesResultCache;
	}

	public (decimal MaxVol, int MaxCount) MaxInView(decimal fixHigh, decimal fixLow, decimal tickSize, bool useWeight = false)
	{
		(decimal MaxVol, int MaxCount) max = (0, 0);
		var w = 0m;

		// Optimization: iterate only over prices with data, not the entire range
		if (_priceVolume.Count > 0)
		{
			foreach (var kvp in _priceVolume)
			{
				var price = kvp.Key;
				if (price < fixLow || price > fixHigh)
					continue;

				var value = kvp.Value;
				if (useWeight)
				{
					var a = value.vol * value.count;
					if (a > w)
					{
						w = a;
						max = value;
					}
				}
				else
				{
					if (value.vol > max.MaxVol)
						max.MaxVol = value.vol;

					if (value.count > max.MaxCount)
						max.MaxCount = value.count;
				}
			}
		}
		else
		{
			lock (_level2Data)
			{
				if (_level2Data.Count != 0)
				{
					foreach (var kvp in _level2Data)
					{
						var price = kvp.Key;
						if (price < fixLow || price > fixHigh)
							continue;

						max.MaxCount = 1;
						if (kvp.Value.Volume > max.MaxVol)
							max.MaxVol = kvp.Value.Volume;
					}
				}
			}
		}

		return max;
	}
	
	public (decimal volume, DataType dataType) Volume(decimal price, MarketDataArg lastAsk, MarketDataArg lastBid, decimal lastPrice)
	{
		var type = DataType.Lvl3;

		lock (_updateLock)
		{
			if (_priceVolume.Count == 0 && _grid.Count == 0)
			{
				type = DataType.Lvl2;

				lock (_level2UpdateLock)
				{
					if (_level2Data.TryGetValue(price, out var value))
					{
						if (value.DataType is ATAS.Indicators.MarketDataType.Ask && price < lastAsk.Price)
							return (0, type);

						if (value.DataType is ATAS.Indicators.MarketDataType.Bid && price > lastBid.Price)
							return (0, type);

						return (value.Volume, type);
					}
				}
			}
			else
			{
				if (_priceVolume.TryGetValue(price, out var value))
					return (value.vol, type);
			}

			return (0, type);
		}
	}

	public bool Update(MarketDataArg depth)
	{
		lock (_level2UpdateLock)
		{
			if (_level2Data.Count == 0)
				return false;

			if ((depth.IsAsk || depth.IsBid) &&
			    depth.DataType is ATAS.Indicators.MarketDataType.Ask or ATAS.Indicators.MarketDataType.Bid)
			{
				var isNewPrice = !_level2Data.ContainsKey(depth.Price);
				if (isNewPrice)
					_level2Data.TryAdd(depth.Price, depth);
				_level2Data[depth.Price] = depth;

				if (isNewPrice)
					InvalidateLevel2PriceRange(depth.Price);

				return true;
			}

			return true;
		}
	}

	public void Load(IEnumerable<MarketDataArg>? getMarketDepthSnapshot)
	{
		lock (_level2UpdateLock)
		{
			_level2Data.Clear();
			_isLevel2PriceRangeDirty = true;

			if (getMarketDepthSnapshot == null)
				return;

			var marketDepthSnapshot = getMarketDepthSnapshot as MarketDataArg[] ?? getMarketDepthSnapshot.ToArray();
			var array = marketDepthSnapshot.ToArray();

			if (marketDepthSnapshot.Any())
			{
				foreach (var depth in array)
				{
					if (!_level2Data.ContainsKey(depth.Price))
						_level2Data.TryAdd(depth.Price, depth);
					_level2Data[depth.Price] = depth;
				}
			}
		}
	}

	#endregion

	#region Private methods

	private void Reset()
	{
		_grid.Clear();
		_priceVolume.Clear();
		_level2Data.Clear();
		_isGridPriceRangeDirty = true;
		_isLevel2PriceRangeDirty = true;
	}

	private void UpdateList(IEnumerable<MarketByOrder> orders)
	{
		foreach (var order in orders)
		{
			if (!_mboHistory.TryGetValue(order.ExchangeOrderId, out var existedOrder))
			{
				existedOrder = order;
				_mboHistory[order.ExchangeOrderId] = existedOrder;
            }

			if(order.Type == MarketByOrderUpdateTypes.Change)
			{
				if (existedOrder.Price != order.Price)
				{
					var orderToDelete = new MarketByOrder()
					{
						ExchangeOrderId = order.ExchangeOrderId,
						Priority = order.Priority,
						Security = order.Security,
						Side = order.Side,
						Volume = order.Volume,
						Type = MarketByOrderUpdateTypes.Delete,
						Time = order.Time,
						Price = existedOrder.Price
                    };

					_priceVolume[existedOrder.Price] = _grid[existedOrder.Price].UpdateOrder(orderToDelete);
                }
			}

			_mboHistory[order.ExchangeOrderId] = order;

			if (order.Type == MarketByOrderUpdateTypes.Delete)
				_mboHistory.Remove(order.ExchangeOrderId, out _);

            if (!_grid.ContainsKey(order.Price))
            {
				_grid.TryAdd(order.Price, new RowItem());
				// Invalidate cache only when adding a new price
				InvalidateGridPriceRange(order.Price);
            }

            _priceVolume[order.Price] = _grid[order.Price].UpdateOrder(order);
		}
	}

	private void InvalidateGridPriceRange(decimal newPrice)
	{
		// Optimization: if new price extends the range, just update bounds
		// without full cache rebuild
		if (!_isGridPriceRangeDirty && _grid.Count > 1)
		{
			if (newPrice < _gridMinPrice)
			{
				_gridMinPrice = newPrice;
				return;
			}
			if (newPrice > _gridMaxPrice)
			{
				_gridMaxPrice = newPrice;
				return;
			}
		}
		_isGridPriceRangeDirty = true;
	}

	private void InvalidateLevel2PriceRange(decimal newPrice)
	{
		// Optimization: if new price extends the range, just update bounds
		if (!_isLevel2PriceRangeDirty && _level2Data.Count > 1)
		{
			if (newPrice < _level2MinPrice)
			{
				_level2MinPrice = newPrice;
				return;
			}
			if (newPrice > _level2MaxPrice)
			{
				_level2MaxPrice = newPrice;
				return;
			}
		}
		_isLevel2PriceRangeDirty = true;
	}

	#endregion
}

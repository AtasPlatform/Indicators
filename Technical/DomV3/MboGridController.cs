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
	}

	#endregion

	#region Public methods

	public void Update(MarketByOrder order)
	{
		Order = order;
	}

	public void RemoveFlag(bool state)
	{
		IsNeedToBeRemove = true;
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
				if (!_orders.ContainsKey(order.ExchangeOrderId))
					_orders.TryAdd(order.ExchangeOrderId, new OrderInfo(order));
				
				_orders[order.ExchangeOrderId].Update(order);

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

	public IEnumerable<OrderInfo> GetOrderedData()
	{
		return _orders.OrderBy(e => e.Value.Order.Priority).Select(e => e.Value);
	}

	public void RemoveExpireOrder()
	{
		if (_orders.Any())
		{
			_orders.RemoveWhere(e => e.Value.IsNeedToBeRemove);
		}
	}

	#endregion

	#region Private methods

	private void RemoveAllAgain(MarketDataType type)
	{
		if (_orders.Count > 0)
			_orders.RemoveWhere(e => e.Value.Type != type);
	}

	private void SetRemoveFlag(long orderExchangeOrderId)
	{
		if (_orders.TryGetValue(orderExchangeOrderId, out var order))
		{
			order.RemoveFlag(true);
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

    private readonly ConcurrentDictionary<long, MarketByOrder> _mboHistory = new ConcurrentDictionary<long, MarketByOrder>();
    private readonly ConcurrentDictionary<decimal, RowItem> _grid = new();
	private readonly object _level2UpdateLock = new();
	private readonly Dictionary<decimal, (decimal vol, int count)> _priceVolume = new();
	private readonly object _updateLock = new();
	private readonly ConcurrentDictionary<decimal, MarketDataArg> _level2Data = new();

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
			if (_grid.Any())
				foreach (var item in _grid)
					item.Value.RemoveExpireOrder();
		}
	}

	public (OrderInfo[] Orders, MarketDataType Type) GetItemInRow(decimal price, MarketDataArg lastAsk,
		MarketDataArg lastBid, decimal lastPrice)
	{
		var nullItem = (Array.Empty<OrderInfo>(), MarketDataType.Trade);

		lock (_updateLock)
		{
			if (_grid.Count > 0)
			{
				if (_grid.TryGetValue(price, out var value))
				{
					if (value.Type is MarketDataType.Ask && price < lastAsk.Price)
						return nullItem;

					if (value.Type is MarketDataType.Bid && price > lastBid.Price)
						return nullItem;

					return (value.GetOrderedData().ToArray(), value.Type);
				}
			}
			else
			{
				lock (_level2Data)
				{
					if (_level2Data.Count > 0 && _grid.Count == 0)
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
									Price = value.Price, Volume = value.Volume,
									Type = MarketByOrderUpdateTypes.Snapshot,
									ExchangeOrderId = 0, Priority = 0, Side = type,
									Security = new Security(), Time = value.Time
								});

								return (new[] { order },

								type);
							}
						}
					}
				}
			}
		}

		return nullItem;
	}

	public (decimal MaxVol, int MaxCount) MaxInView(decimal fixHigh, decimal fixLow, decimal tickSize,
		bool useWeight = false)
	{
		(decimal MaxVol, int MaxCount) max = (0, 0);
		var w = 0m;

		for (var price = fixHigh; price >= fixLow; price -= tickSize)
		{
			if (_priceVolume.Count > 0)
			{
				if (_priceVolume.TryGetValue(price, out var value))
				{
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
					if (_level2Data.Count > 0)
					{
						if (_level2Data.TryGetValue(price, out var value))
						{
							max.MaxCount = 1;

							if (value.Volume > max.MaxVol)
								max.MaxVol = value.Volume;
						}
					}
				}
			}
		}

		return max;
	}

	public (decimal volume, DataType dataType) Volume(decimal price,
		MarketDataArg lastAsk,
		MarketDataArg lastBid, decimal lastPrice)
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
				if (!_level2Data.ContainsKey(depth.Price))
					_level2Data.TryAdd(depth.Price, depth);
				_level2Data[depth.Price] = depth;

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
				_grid.TryAdd(order.Price, new RowItem());

            _priceVolume[order.Price] = _grid[order.Price].UpdateOrder(order);
		}
	}

	#endregion
}
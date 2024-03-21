namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

using ATAS.DataFeedsCore;
using ATAS.Indicators;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Utils.Common.Collections.Synchronized;
using Utils.Common.Logging;

public partial class DomV3
{
	#region Nested types

	public class MboController
	{
		#region Nested types

		public delegate void UpdateChanel();

		#endregion

		#region Fields

		private readonly IMarketByOrdersCache _manager;

		private readonly SyncDictionary<long, decimal> _orderLevelController = new();
		private readonly ConcurrentDictionary<decimal, MboGridRow> _syncGridLevel;
		private readonly object _uiLock = new();

		private readonly ConcurrentQueue<MboPack> _updateChanel = new();
		private readonly object _updateLocker = new();

		public readonly Timer? CleanerTimer;
		private readonly ConcurrentQueue<DomV3Indicator.DeletedOrderFlag> deletedOrder;

		private readonly RenderStringFormat _format = new()
		{
			Alignment = StringAlignment.Center, Trimming = StringTrimming.None,
			FormatFlags = StringFormatFlags.FitBlackBox, LineAlignment = StringAlignment.Center
		};

		private bool _isFirstTime;
		private ulong _lastKey;

		private RenderFont _normalFont = new("Arial", 15, FontStyle.Regular, GraphicsUnit.Point);
		public Color AskColor = Color.Red;
		public Color BidColor = Color.Green;

		#endregion

		#region Events

		public event UpdateChanel OnChanelUpdate;

		#endregion

		#region ctor

		public MboController(IMarketByOrdersCache manager)
		{
			_isFirstTime = true;
			_manager = manager;
			_updateChanel.Clear();
			_syncGridLevel = new ConcurrentDictionary<decimal, MboGridRow>();
			OnChanelUpdate += AddOrdUpdate;

			deletedOrder = new ConcurrentQueue<DomV3Indicator.DeletedOrderFlag>();
			CleanerTimer = new Timer();
			CleanerTimer.Interval = 1000;
			CleanerTimer.Elapsed += TimerCallback;
			CleanerTimer.AutoReset = false;
		}

		#endregion

		#region Public methods

		public void Dispose()
		{
			OnChanelUpdate -= AddOrdUpdate;
			_updateChanel.Clear();
			_syncGridLevel.Clear();
			CleanerTimer?.Stop();
			CleanerTimer?.Dispose();
		}

		public void AddMarketByOrder(IEnumerable<MarketByOrder> items)
		{
			Update(items);
		}

		public void AddTrades(IEnumerable<MarketDataArg> trades)
		{
			if (_isFirstTime)
				return;

			if (ReferenceEquals(trades, null))
				return;

			var marketDataArgs = trades as MarketDataArg[] ?? trades.ToArray();

			if (!marketDataArgs.Any())
				return;

			_updateChanel.Enqueue(new MboPack(marketDataArgs));

			RunOnChanelUpdate();
		}

		public void RenderSnapshot(RenderContext context, IIndicatorContainer container, IChart chartInfo)
		{
			lock (_uiLock)
			{
				var high = chartInfo.PriceChartContainer.High + chartInfo.PriceChartContainer.Step;
				var low = chartInfo.PriceChartContainer.Low - chartInfo.PriceChartContainer.Step;
				var copy = new ConcurrentDictionary<decimal, MboGridRow>(_syncGridLevel.Where(e => e.Key <= high && e.Key >= low));

				if (!copy.Any())
					return;

				var rh = (int)chartInfo.PriceChartContainer.PriceRowHeight;

				var maxVolByLen = copy.Values.Max(e =>
				{
					var f1 = e.DeletedVolume.ToString("0.##").Length;
					var f2 = e.FillVolume.ToString("0.##").Length;
					var f3 = e.RemainingVolume.ToString("0.##").Length;
					var f4 = e.TotalVolume.ToString("0.##").Length;
					return Math.Max(Math.Max(f1, f2), Math.Max(f3, f4));
				});

				var totalVolUseInRow = copy.Max(e => e.Value.TotalVolume);

				var f = GetTextSize(context, rh, totalVolUseInRow);
				_normalFont = new RenderFont("Arial", Math.Min(f, maxVolByLen * 10), FontStyle.Regular, GraphicsUnit.Point);

				var size = context.MeasureString(new string('9', maxVolByLen), _normalFont);

				var offset = size.Width + 10;

				var area = (int)(container.RelativeRegion.Width * 0.9m);
				var right = container.RelativeRegion.Right - offset;
				var left = right - area;

				var widthArea = right - left;

				if (rh > 15)
				{
					//show with details
					var boxes = copy.Values.SelectMany(e =>
						e.DetailsView(context, _normalFont, chartInfo, container, widthArea, totalVolUseInRow, offset, BidColor, AskColor));

					foreach (var box in boxes)
						box.Render(context, _normalFont, _format);
				}
				else
				{
					//minimal show
					var boxes = copy.Values.SelectMany(e => e.BigView(chartInfo, container, widthArea, totalVolUseInRow, offset, BidColor, AskColor));

					foreach (var box in boxes)
						box.Render(context, _normalFont, _format);
				}
			}
		}

		#endregion

		#region Private methods

		private void TimerCallback(object? sender, ElapsedEventArgs e)
		{
			if (sender == null)
				return;

			CleanerTimer?.Stop();

			try
			{
				if (deletedOrder.Count > 0)
				{
					while (deletedOrder.TryDequeue(out var order))
					{
						if (_syncGridLevel.TryGetValue(order.PriceRow, out var rows))
						{
							if (ReferenceEquals(rows, null))
								continue;

							rows.RemoveOrder(order.OrderId);
							this.LogInfo("remove by timer");
						}
					}
				}
				//cleanChanel
			}
			catch (Exception es)
			{
				this.LogWarn(" " + es.Message);
			}

			CleanerTimer?.Start();
		}

		private void RunOnChanelUpdate()
		{
			Task.Run(() =>
			{
				try
				{
					OnChanelUpdate?.Invoke();
				}
				catch (Exception excp)
				{
					this.LogError("OnChanelUpdate error.", excp);
				}
			});
        }


        private void Update(IEnumerable<MarketByOrder> orders)
		{
			if (ReferenceEquals(orders, null))
				return;

			var marketByOrders = orders as MarketByOrder[] ?? orders.ToArray();

			if (!marketByOrders.Any())
				return;

			if (_isFirstTime)
			{
				_updateChanel.Enqueue(new MboPack(_manager.MarketByOrders));
				_isFirstTime = false;

				if (CleanerTimer != null)
					CleanerTimer.Enabled = true;
			}

			_updateChanel.Enqueue(new MboPack(marketByOrders));

			RunOnChanelUpdate();
		}

		// public void AddForTest(MboPack update)
		// {
		//     if (ReferenceEquals(update, null)) return;
		//     _updateChanel.Enqueue(update);
		//     Task.Run(() => OnChanelUpdate?.Invoke());
		// }

		private void AddOrdUpdate()
		{
			lock (_updateLocker)
			{
				if (_updateChanel.Count == 0)
					return;

				if (ReferenceEquals(_syncGridLevel, null))
					return;

				if (_updateChanel.TryDequeue(out var mboPack))
				{
					if (_lastKey == mboPack.KeyIndex)
					{
						this.LogWarn("same key");
						return;
					}

					_lastKey = mboPack.KeyIndex;

					if (ReferenceEquals(mboPack, null))
						return;

					if (ReferenceEquals(mboPack.Orders, null) && ReferenceEquals(mboPack.Trades, null))
						return;

					if (!ReferenceEquals(mboPack.Orders, null) && mboPack.Orders.Any())
					{
						//need to update Dom
						foreach (var order in mboPack.Orders)
						{
							var key = order.ExchangeOrderId;
							var price = order.Price;

							var needToChangeLevel = false;

							if (!_orderLevelController.ContainsKey(key))
							{
								if (order.Type is MarketByOrderUpdateTypes.Change or MarketByOrderUpdateTypes.New or MarketByOrderUpdateTypes.Snapshot)
								{
									if (!(order.Type is MarketByOrderUpdateTypes.Change && order.Volume == 0))
										_orderLevelController.Add(key, price);
								}
							}
							else
							{
								if (_orderLevelController[key] != price)
									needToChangeLevel = true;
							}

							if (needToChangeLevel)
							{
								var oldPriceOfOrder = _orderLevelController[key];

								if (_syncGridLevel.TryGetValue(oldPriceOfOrder, out var row))
								{
									var orderData = row.CutOrderFromLevelByKey(key);

									if (orderData != null)
									{
										if (!_syncGridLevel.ContainsKey(price))
											_syncGridLevel.TryAdd(price, new MboGridRow(order, mboPack.KeyIndex));
										_syncGridLevel[price].AttachedOrderToThisLevel(orderData);
									}
								}
							}

							if (!_syncGridLevel.ContainsKey(order.Price))
								_syncGridLevel.TryAdd(order.Price, new MboGridRow(order, mboPack.KeyIndex));
							var deleteList = _syncGridLevel[order.Price].Update(order, mboPack.KeyIndex);

							if (deleteList.Any())
							{
								foreach (var l in deleteList)
									deletedOrder.Enqueue(l);
							}

							_orderLevelController[key] = price;

							if (order.Volume == 0 || order.Type is MarketByOrderUpdateTypes.Delete)
								_orderLevelController.Remove(key);
						}
					}

					// we dont need do any extra for this part
					if (!ReferenceEquals(mboPack.Trades, null) && mboPack.Trades.Any())
					{
						//need to update trade
						foreach (var trade in mboPack.Trades)
						{
							var id = trade.ExchangeOrderId ?? 0;
							var price = trade.Price;
							var volume = trade.Volume;

							if (id == 0 || volume == 0 || !_orderLevelController.ContainsKey(id))
								continue;

							var oldPrice = _orderLevelController[id];

							if (_syncGridLevel.TryGetValue(price, out var level1))
							{
								var deleteList = level1.Update(trade, mboPack.KeyIndex);

								if (deleteList.Any())
								{
									foreach (var l in deleteList)
										deletedOrder.Enqueue(l);
								}
							}

							if (oldPrice != price)
							{
								if (_syncGridLevel.TryGetValue(oldPrice, out var level2))
								{
									var deleteList = level2.Update(trade, mboPack.KeyIndex);

									if (deleteList.Any())
									{
										foreach (var l in deleteList)
											deletedOrder.Enqueue(l);
									}
								}
							}
						}
					}
				}
			}
		}

		private int GetTextSize(RenderContext context, int height, decimal vol)
		{
			for (var emSize = 10; emSize > 0; --emSize)
			{
				if (context.MeasureString(vol.ToString("0.##"), new RenderFont("Arial", emSize)).Height < height + 4)
					return emSize;
			}

			return 0;
		}

		#endregion
	}

	#endregion
}
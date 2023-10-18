namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using ATAS.DataFeedsCore;
using ATAS.Indicators;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Utils.Common.Collections;

using MarketDataType = ATAS.DataFeedsCore.MarketDataType;

public partial class DomV3
{
	#region Nested types

	public class MboGridRow
	{
		#region Properties

		public MarketDataType Side { get; set; }

		public decimal Price { get; }

		public ulong LastUpdateIndex { private set; get; }

		public SortedDictionary<long, MboOrder> Orders { get; } = new();

		public decimal TotalVolume => Orders.Any() ? Orders.Values.Sum(e => e.TotalVolume) : 0;

		public decimal RemainingVolume => Orders.Any() ? Orders.Values.Sum(e => e.MarkAsRemove ? 0 : e.RemainingVolume) : 0;

		public decimal DeletedVolume => Orders.Any() ? Orders.Values.Sum(e => e.DeletedVolume) : 0;

		public decimal FillVolume => Orders.Any() ? Orders.Values.Sum(e => e.FillVolume) : 0;

		#endregion

		#region ctor

		public MboGridRow(MarketByOrder order, ulong index)
		{
			Side = order.Side;
			Price = order.Price;
			LastUpdateIndex = index;
		}

		#endregion

		#region Public methods

		public IEnumerable<DomV3Indicator.DeletedOrderFlag> Update(MarketByOrder order, ulong mboKey)
		{
			LastUpdateIndex = mboKey;

			if (Side != order.Side)
			{
				Orders.Clear();
				Side = order.Side;
			}

			if (order.Volume is 0 && order.Type != MarketByOrderUpdateTypes.Delete)
				order.Type = MarketByOrderUpdateTypes.Delete;

			var id = order.ExchangeOrderId;

			// CheckForDeletedOrder();
			switch (order.Type)
			{
				case MarketByOrderUpdateTypes.Snapshot:
					if (Orders.ContainsKey(id))
						Orders.Remove(id);
					Orders.Add(id, new MboOrder(order));
					CheckForDeletedOrder();
					break;
				case MarketByOrderUpdateTypes.New:
					if (Orders.ContainsKey(id))
						Orders.Remove(id);
					Orders.Add(id, new MboOrder(order));
					CheckForDeletedOrder();
					break;
				case MarketByOrderUpdateTypes.Change:
					if (!Orders.ContainsKey(id))
						Orders.Add(id, new MboOrder(order));
					else
						Orders[id].Update(order);
					break;
				case MarketByOrderUpdateTypes.Delete:
					if (Orders.TryGetValue(id, out var o))
						o.Update(order);
					break;
			}

			return Orders.Where(e => e.Value.MarkAsRemove).Select(e => new DomV3Indicator.DeletedOrderFlag
				{ OrderId = e.Key, PriceRow = Price });
		}

		public IEnumerable<DomV3Indicator.DeletedOrderFlag> Update(MarketDataArg trade, ulong mboKey)
		{
			if (trade.ExchangeOrderId.HasValue)
			{
				LastUpdateIndex = mboKey;
				var id = trade.ExchangeOrderId.Value;

				if (Orders.TryGetValue(id, out var order))
					order.Update(trade);
			}

			return Orders.Where(e => e.Value.MarkAsRemove).Select(e => new DomV3Indicator.DeletedOrderFlag
				{ OrderId = e.Key, PriceRow = Price });
		}

		public void AttachedOrderToThisLevel(MboOrder? orderData)
		{
			if (orderData == null)
				return;

			Orders[orderData.Id] = orderData;
		}

		public MboOrder? CutOrderFromLevelByKey(long key)
		{
			return Orders?.TryGetAndRemove(key) ?? null;
		}

		//sum box + Orders box[]
		public IEnumerable<MboRectangle> DetailsView(RenderContext context, RenderFont font, IChart chart, IIndicatorContainer container, int widthArea,
			decimal maxVolumeInRow, int offset, Color bidColor, Color askColor)
		{
			var renderList = new List<MboRectangle>();

			var right = container.RelativeRegion.Right;
			var color = Side == MarketDataType.Bid ? bidColor : Side == MarketDataType.Ask ? askColor : Color.Azure;

			renderList.Add(GetSumBox(chart, offset, right));

			if (TotalVolume > 0)
			{
				var widthPerVol = (int)(widthArea * (TotalVolume / maxVolumeInRow) / TotalVolume);
				var y1 = renderList.Last().Y1;
				var y2 = renderList.Last().Y2;
				var startPoint = renderList.Last().X1;

				// var data = Orders.OrderBy(e => e.Key).Select(e => e.Value);
				// var data = Orders.OrderByDescending(e => e.Value.TotalVolume).Select(e => e.Value);
				// var data = Orders.OrderByDescending(e => e.Value.TotalVolume).Select(e => e.Value);
				var data = Orders.OrderBy(e => e.Value.Priority).Select(e => e.Value);

				var first = true;

				foreach (var mboOrder in data)
				{
					var boxes = mboOrder.View(context, font, startPoint, widthPerVol, y1, y2, color, first).ToArray();
					startPoint = boxes.Min(e => e.X1);
					first = false;
					renderList.AddRange(boxes);
				}
			}

			return renderList;
		}

		//sum box + row volume 
		public IEnumerable<MboRectangle> BigView(IChart chart, IIndicatorContainer container, int widthArea, decimal maxVolumeInRow, int offset, Color bidColor,
			Color askColor)
		{
			var renderList = new List<MboRectangle>();
			var right = container.RelativeRegion.Right;
			var color = Side == MarketDataType.Bid ? bidColor : Side == MarketDataType.Ask ? askColor : Color.Azure;

			renderList.Add(GetSumBox(chart, offset, right));

			var y1 = renderList.Last().Y1;
			var y2 = renderList.Last().Y2;

			var startPoint = renderList.Last().X1;

			var vol = RemainingVolume + FillVolume;

			if (vol > 0)
			{
				var cellWidth = (int)(widthArea * (Math.Max(RemainingVolume, FillVolume) / maxVolumeInRow));

				renderList.Add(new MboRectangle(true)
				{
					X1 = startPoint - cellWidth, X2 = startPoint,
					Y1 = y1, Y2 = y2,
					Data = (!Orders.Any() ? 0 : Orders.Count(e => !e.Value.MarkAsRemove)).ToString(),
					Padding = new MboRectanglePadding(1, 0, 1, 1),
					Pen = new RenderPen(color)
				});
				var x1 = renderList.Last().X1;
				var x2 = renderList.Last().X2;

				if (FillVolume > 0)
				{
					renderList.Add(new MboRectangle(true)
					{
						X1 = x1, X2 = x2,
						Y1 = y2 - (int)Math.Max(Math.Abs(y1 - y2) * 0.2, 5), Y2 = y2,
						Pen = new RenderPen(Color.Cyan),
						Padding = new MboRectanglePadding(0, 0, 0, 0)
					});
				}
			}

			return renderList;
		}

		public void RemoveOrder(long orderId)
		{
			if (Orders.ContainsKey(orderId))
				Orders.Remove(orderId);
		}

		#endregion

		#region Private methods

		private void CheckForDeletedOrder()
		{
			Orders.RemoveWhere(e => e.Value.IsRemove);
		}

		private MboRectangle GetSumBox(IChart chartInfo, int offset, int right)
		{
			var x = right - offset;
			var y = chartInfo.GetYByPrice(Price);

			return new MboRectangle(true)
			{
				X1 = x, X2 = right,
				Y1 = y, Y2 = y + (int)chartInfo.PriceChartContainer.PriceRowHeight,
				Pen = RenderPens.DarkSlateGray, BorderColor = RenderPens.Wheat,
				Data = RemainingVolume
			};
		}

		#endregion
	}

	#endregion
}
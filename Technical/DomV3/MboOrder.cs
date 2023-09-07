namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using ATAS.DataFeedsCore;
using ATAS.Indicators;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Utils.Common.Collections.Synchronized;

public partial class DomV3
{
	#region Nested types

	public class MboOrder
	{
		#region Fields

		private readonly int MaxRateUpdate = 1;
		private decimal _deleteAmount;
		private int _lifeCycle;

		#endregion

		#region Properties

		private MarketByOrder BaseOrder { get; }

		private SyncDictionary<long, MarketDataArg> OrderExecution { get; }

		public bool MarkAsRemove { get; private set; }

		public bool IsRemove
		{
			get
			{
				if (MarkAsRemove)
				{
					if (_lifeCycle >= MaxRateUpdate)
						return true;

					_lifeCycle += 1;
				}

				return false;
			}
		}

		public decimal TotalVolume => BaseOrder.Volume;

		public decimal RemainingVolume => Math.Max(TotalVolume - FillVolume, 0);

		public decimal DeletedVolume
		{
			get
			{
				if (!MarkAsRemove)
					return 0;

				if (_deleteAmount == 0)
					return RemainingVolume;

				return _deleteAmount;
			}
		}

		public decimal FillVolume
		{
			get
			{
				if (!ReferenceEquals(OrderExecution, null) && OrderExecution.Any())
					return OrderExecution.Sum(e => Math.Abs(e.Value.Volume));

				return 0;
			}
		}

		public long Priority => BaseOrder.Priority;

		public long Id => BaseOrder.ExchangeOrderId;

		#endregion

		#region ctor

		public MboOrder(MarketByOrder order)
		{
			BaseOrder = order;
			OrderExecution = new SyncDictionary<long, MarketDataArg>();
		}

		#endregion

		#region Public methods

		public void Update(MarketByOrder order)
		{
			//re init in first of market
			if (BaseOrder.Priority != order.Priority)
			{
				BaseOrder.Priority = order.Priority;
				BaseOrder.Price = order.Price;
				BaseOrder.Volume = FillVolume + order.Volume;
			}

			if (order.Volume == 0 || order.Type == MarketByOrderUpdateTypes.Delete || TotalVolume == FillVolume || RemainingVolume == 0)
				MarkAsRemove = true;

			if (order is { Type: MarketByOrderUpdateTypes.Delete, Volume: > 0 })
				_deleteAmount = order.Volume;

			if (MarkAsRemove)
				_lifeCycle = 0;
		}

		public void Update(MarketDataArg trade)
		{
			if (!trade.ExchangeOrderId.HasValue || !trade.AggressorExchangeOrderId.HasValue)
				return;

			var aeId = trade.AggressorExchangeOrderId.Value;
			OrderExecution[aeId] = trade;

			if (TotalVolume == FillVolume || RemainingVolume == 0)
			{
				MarkAsRemove = true;
				_lifeCycle = 0;
			}
		}

		public IEnumerable<MboRectangle> View(RenderContext context, RenderFont font, int startPoint, int widthPerVol, int y1, int y2, Color color,
			bool firstFromRight)
		{
			var fillColor = color;
			var deleteColor = Color.Wheat;

			MboRectanglePadding padding = new()
			{
				Left = 2, Top = 1,
				Right = firstFromRight ? 0 : 2, Bottom = 1
			};

			var views = new List<MboRectangle>();

			//both side is hallow 
			if (MarkAsRemove)
			{
				//delete box
				if (FillVolume == 0)
				{
					//single Box of remove
					var w = GetWidth(widthPerVol * TotalVolume, context, font, TotalVolume);

					views.Add(new MboRectangle(false)
					{
						X1 = startPoint - w, X2 = startPoint,
						Y1 = y1, Y2 = y2,
						Pen = new RenderPen(color),
						Data = TotalVolume, Padding = padding
					});
				}
				else
				{
					//two box  left for fill right for delete
					var x1 = startPoint;

					if (DeletedVolume > 0)
					{
						var remainWidth = GetWidth(widthPerVol * DeletedVolume, context, font, DeletedVolume);

						views.Add(new MboRectangle(true)
						{
							X1 = startPoint - remainWidth, X2 = startPoint,
							Y1 = y1, Y2 = y2,
							Data = DeletedVolume,
							Pen = new RenderPen(color),
							Padding = padding with { Left = 0 }
						});

						views.Add(new MboRectangle(true)
						{
							X1 = views.Last().X1, X2 = views.Last().X2,
							Y1 = y2 - (int)Math.Max(Math.Abs(y1 - y2) * 0.2, 5), Y2 = y2,
							Pen = new RenderPen(deleteColor),
							Padding = padding with { Left = 0, Top = 0 }
						});

						x1 = views.Last().X1;
					}

					var fillWidth = GetWidth(widthPerVol * FillVolume, context, font, FillVolume);

					views.Add(new MboRectangle(false)
					{
						X1 = x1 - fillWidth, X2 = x1,
						Y1 = y1, Y2 = y2,
						Data = FillVolume,
						Pen = new RenderPen(color),
						Padding = padding with { Right = 0 }
					});

					views.Add(new MboRectangle(true)
					{
						X1 = views.Last().X1, X2 = views.Last().X2,
						Y1 = y2 - (int)Math.Max(Math.Abs(y1 - y2) * 0.2, 5), Y2 = y2,
						Pen = new RenderPen(fillColor),
						Padding = padding with { Right = 0, Top = 0 }
					});
				}
			}
			else
			{
				//normal box
				if (FillVolume == 0)
				{
					//single Box of solid Ramin Volume
					var w = GetWidth(widthPerVol * TotalVolume, context, font, TotalVolume);

					views.Add(new MboRectangle(true)
					{
						X1 = startPoint - w, X2 = startPoint,
						Y1 = y1, Y2 = y2,
						Pen = new RenderPen(color),
						Data = TotalVolume, Padding = padding
					});
				}
				else
				{
					//two box  left for fill right for Ramin volume
					var x1 = startPoint;

					if (RemainingVolume > 0)
					{
						var remainWidth = GetWidth(widthPerVol * RemainingVolume, context, font, RemainingVolume);

						views.Add(new MboRectangle(true)
						{
							X1 = startPoint - remainWidth, X2 = startPoint,
							Y1 = y1, Y2 = y2,
							Data = RemainingVolume,
							Pen = new RenderPen(color),
							Padding = padding with { Left = 0 }
						});
						x1 = views.Last().X1;
					}

					var fillWidth = GetWidth(widthPerVol * FillVolume, context, font, FillVolume);

					views.Add(new MboRectangle(false)
					{
						X1 = x1 - fillWidth, X2 = x1,
						Y1 = y1, Y2 = y2,
						Data = FillVolume,
						Pen = new RenderPen(color),
						Padding = padding with { Right = 0 }
					});

					views.Add(new MboRectangle(true)
					{
						X1 = views.Last().X1, X2 = views.Last().X2,
						Y1 = y2 - (int)Math.Max(Math.Abs(y1 - y2) * 0.2, 5), Y2 = y2,
						Pen = new RenderPen(fillColor),
						Padding = padding with { Right = 0, Top = 0 }
					});
				}
			}

			return views;
		}

		public int GetWidth(decimal w, RenderContext context, RenderFont font, decimal vol)
		{
			var width = 0;

			try
			{
				width = context.MeasureString(vol.ToString("0.##"), font).Width + 2;
			}
			catch
			{
				width = 0;
			}

			return (int)Math.Max(w, width);
		}

		#endregion
	}

	#endregion
}
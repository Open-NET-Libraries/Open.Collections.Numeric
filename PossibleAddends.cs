using Open.Disposable;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Open.Collections.Numeric
{
	public class PossibleAddends : DisposableBase
	{
		public PossibleAddends()
		{
		}

		readonly ConcurrentDictionary<int, ConcurrentDictionary<int, IReadOnlyList<IReadOnlyList<int>>>> Cache = new();

		public IReadOnlyList<IReadOnlyList<int>> UniqueAddendsFor(int sum, int count)
			=> Cache
				.GetOrAdd(count, key => new ConcurrentDictionary<int, IReadOnlyList<IReadOnlyList<int>>>())
				.GetOrAdd(sum, key => GetUniqueAddends(sum, count).Memoize());

		public IEnumerable<IReadOnlyList<int>> GetUniqueAddends(int sum, int count)
		{
			if (count > int.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot be greater than signed 32 bit integer maximum.");
			if (count < 2 || sum < 3)
				yield break;


			if (count == 2)
			{
				int i = 0;
			loop2:
				i++;
				if (i * 2 >= sum) yield break;
				yield return ImmutableArray.Create(i, sum - i);

				goto loop2;
			}

			{
				int i = 2;
				var builder = ImmutableArray.CreateBuilder<int>();

				while (++i < sum)
				{
					var next = sum - i;
					var addends = UniqueAddendsFor(i, count - 1);
					foreach (var a in addends)
					{
						builder.Capacity = count;
						if (a[a.Count - 1] >= next) continue;
						builder.AddRange(a);
						builder.Add(next);
						yield return builder.MoveToImmutable();
					}
				}
			}
		}

		protected override void OnDispose() => Cache.Clear();
	}
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace WebAuthProto
{
	public interface ICredentialStorage
	{
		Task<StoredCredential[]> GetUserCredentialsAsync(byte[] userId);
		Task<StoredCredential> GetCredentialAsync(byte[] userId, byte[] credentialId);

		Task AddCredentialToUserAsync(byte[] userId, StoredCredential credential);

		Task UpdateCounterAsync(byte[] userId, byte[] credentialId, uint counter);
	}

	public class StoredCredential
	{
		public byte[] UserId { get; set; }
		public PublicKeyCredentialDescriptor Descriptor { get; set; }
		public byte[] PublicKey { get; set; }
		public byte[] UserHandle { get; set; }
		public uint SignatureCounter { get; set; }
		public string CredType { get; set; }
		public DateTime RegDate { get; set; }
		public Guid AaGuid { get; set; }
	}

	internal class ByteArrayEquityComparer : IEqualityComparer<byte[]>
	{
		public bool Equals(byte[] x, byte[] y)
		{
			return x.SequenceEqual(y);
		}

		public int GetHashCode(byte[] obj)
		{
			int a = 0;
			for (var i = 0; i < obj.Length; i++) {
				a = (obj[i] << (i % 4)) ^ a;
			}
			return a;
		}
	}

	internal class InMemoryCredentialStorage : ICredentialStorage
	{
		private readonly ConcurrentDictionary<byte[], List<StoredCredential>> storage = new ConcurrentDictionary<byte[], List<StoredCredential>>(new ByteArrayEquityComparer());

		public Task AddCredentialToUserAsync(byte[] userId, StoredCredential credential)
		{
			storage.AddOrUpdate(userId, b =>
			{
				var l = new List<StoredCredential>();
				l.Add(credential);
				return l;
			}, (b, l) =>
			{
				l.Add(credential);
				return l;
			});
			return Task.CompletedTask;
		}

		public Task<StoredCredential[]> GetUserCredentialsAsync(byte[] userId)
		{
			return Task.FromResult(storage.GetOrAdd(userId, b => new List<StoredCredential>()).ToArray());
		}

		public Task<StoredCredential> GetCredentialAsync(byte[] userId, byte[] credentialId)
		{
			if (storage.TryGetValue(userId, out var credentials))
			{
				return Task.FromResult(credentials.FirstOrDefault(c => c.Descriptor.Id.SequenceEqual(credentialId)));
			}
			return Task.FromResult<StoredCredential>(null);
		}

		public Task UpdateCounterAsync(byte[] userId, byte[] credentialId, uint counter)
		{
			if (storage.TryGetValue(userId, out var credentials))
			{
				var cred = credentials.FirstOrDefault(c => c.Descriptor.Id.SequenceEqual(credentialId));
				if (cred != null)
				{
					cred.SignatureCounter = counter;
					return Task.CompletedTask;
				}
			}
			throw new Exception("Bad credential");
		}
	}
}
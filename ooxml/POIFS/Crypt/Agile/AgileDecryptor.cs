/* ====================================================================
   Licensed to the Apache Software Foundation (ASF) under one or more
   contributor license agreements.  See the NOTICE file distributed with
   this work for Additional information regarding copyright ownership.
   The ASF licenses this file to You under the Apache License, Version 2.0
   (the "License"); you may not use this file except in compliance with
   the License.  You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
==================================================================== */
namespace NPOI.POIFS.Crypt.Agile
{
    using System;
    using System.IO;
    using NPOI.POIFS.Crypt;

    using NPOI.POIFS.FileSystem;
    using NPOI.Util;
    using Org.BouncyCastle.X509;

    /**
     * Decryptor implementation for Agile Encryption
     */
    public class AgileDecryptor : Decryptor {
        private long _length = -1;

        protected internal static byte[] kVerifierInputBlock;
        protected internal static byte[] kHashedVerifierBlock;
        protected internal static byte[] kCryptoKeyBlock;
        protected internal static byte[] kIntegrityKeyBlock;
        protected internal static byte[] kIntegrityValueBlock;

        static AgileDecryptor()
        {
            kVerifierInputBlock =
                new byte[] { (byte)0xfe, (byte)0xa7, (byte)0xd2, (byte)0x76,
                         (byte)0x3b, (byte)0x4b, (byte)0x9e, (byte)0x79 };
            kHashedVerifierBlock =
                new byte[] { (byte)0xd7, (byte)0xaa, (byte)0x0f, (byte)0x6d,
                         (byte)0x30, (byte)0x61, (byte)0x34, (byte)0x4e };
            kCryptoKeyBlock =
                new byte[] { (byte)0x14, (byte)0x6e, (byte)0x0b, (byte)0xe7,
                         (byte)0xab, (byte)0xac, (byte)0xd0, (byte)0xd6 };
            kIntegrityKeyBlock =
                new byte[] { (byte)0x5f, (byte)0xb2, (byte)0xad, (byte)0x01,
                         (byte)0x0c, (byte)0xb9, (byte)0xe1, (byte)0xf6 };
            kIntegrityValueBlock =
                new byte[] { (byte)0xa0, (byte)0x67, (byte)0x7f, (byte)0x02,
                         (byte)0xb2, (byte)0x2c, (byte)0x84, (byte)0x33 };
        }

        protected internal AgileDecryptor(AgileEncryptionInfoBuilder builder)
            : base(builder)
        {
            
        }

        /**
         * Set decryption password
         */
        public override bool VerifyPassword(String password) {
            AgileEncryptionVerifier ver = (AgileEncryptionVerifier)builder.GetVerifier();
            AgileEncryptionHeader header = (AgileEncryptionHeader)builder.GetHeader();
            HashAlgorithm hashAlgo = header.HashAlgorithm;
            CipherAlgorithm cipherAlgo = header.CipherAlgorithm;
            int blockSize = header.BlockSize;
            int keySize = header.KeySize / 8;

            byte[] pwHash = CryptoFunctions.HashPassword(password, ver.HashAlgorithm, ver.Salt, ver.SpinCount);

            /**
             * encryptedVerifierHashInput: This attribute MUST be generated by using the following steps:
             * 1. Generate a random array of bytes with the number of bytes used specified by the saltSize
             *    attribute.
             * 2. Generate an encryption key as specified in section 2.3.4.11 by using the user-supplied password,
             *    the binary byte array used to create the saltValue attribute, and a blockKey byte array
             *    consisting of the following bytes: 0xfe, 0xa7, 0xd2, 0x76, 0x3b, 0x4b, 0x9e, and 0x79.
             * 3. Encrypt the random array of bytes generated in step 1 by using the binary form of the saltValue
             *    attribute as an Initialization vector as specified in section 2.3.4.12. If the array of bytes is not an
             *    integral multiple of blockSize bytes, pad the array with 0x00 to the next integral multiple of
             *    blockSize bytes.
             * 4. Use base64 to encode the result of step 3.
             */
            byte[] verfierInputEnc = hashInput(builder, pwHash, kVerifierInputBlock, ver.EncryptedVerifier, Cipher.DECRYPT_MODE);
            SetVerifier(verfierInputEnc);
            MessageDigest hashMD = CryptoFunctions.GetMessageDigest(hashAlgo);
            byte[] verifierHash = hashMD.Digest(verfierInputEnc);

            /**
             * encryptedVerifierHashValue: This attribute MUST be generated by using the following steps:
             * 1. Obtain the hash value of the random array of bytes generated in step 1 of the steps for
             *    encryptedVerifierHashInput.
             * 2. Generate an encryption key as specified in section 2.3.4.11 by using the user-supplied password,
             *    the binary byte array used to create the saltValue attribute, and a blockKey byte array
             *    consisting of the following bytes: 0xd7, 0xaa, 0x0f, 0x6d, 0x30, 0x61, 0x34, and 0x4e.
             * 3. Encrypt the hash value obtained in step 1 by using the binary form of the saltValue attribute as
             *    an Initialization vector as specified in section 2.3.4.12. If hashSize is not an integral multiple of
             *    blockSize bytes, pad the hash value with 0x00 to an integral multiple of blockSize bytes.
             * 4. Use base64 to encode the result of step 3.
             */
            byte[] verifierHashDec = hashInput(builder, pwHash, kHashedVerifierBlock, ver.EncryptedVerifierHash, Cipher.DECRYPT_MODE);
            verifierHashDec = CryptoFunctions.GetBlock0(verifierHashDec, hashAlgo.hashSize);

            /**
             * encryptedKeyValue: This attribute MUST be generated by using the following steps:
             * 1. Generate a random array of bytes that is the same size as specified by the
             *    Encryptor.KeyData.keyBits attribute of the parent element.
             * 2. Generate an encryption key as specified in section 2.3.4.11, using the user-supplied password,
             *    the binary byte array used to create the saltValue attribute, and a blockKey byte array
             *    consisting of the following bytes: 0x14, 0x6e, 0x0b, 0xe7, 0xab, 0xac, 0xd0, and 0xd6.
             * 3. Encrypt the random array of bytes generated in step 1 by using the binary form of the saltValue
             *    attribute as an Initialization vector as specified in section 2.3.4.12. If the array of bytes is not an
             *    integral multiple of blockSize bytes, pad the array with 0x00 to an integral multiple of
             *    blockSize bytes.
             * 4. Use base64 to encode the result of step 3.
             */
            byte[] keyspec = hashInput(builder, pwHash, kCryptoKeyBlock, ver.EncryptedKey, Cipher.DECRYPT_MODE);
            keyspec = CryptoFunctions.GetBlock0(keyspec, keySize);
            SecretKeySpec secretKey = new SecretKeySpec(keyspec, ver.CipherAlgorithm.jceId);

            /**
             * 1. Obtain the intermediate key by decrypting the encryptedKeyValue from a KeyEncryptor
             *    Contained within the KeyEncryptors sequence. Use this key for encryption operations in the
             *    remaining steps of this section.
             * 2. Generate a random array of bytes, known as Salt, of the same length as the value of the
             *    KeyData.HashSize attribute.
             * 3. Encrypt the random array of bytes generated in step 2 by using the binary form of the
             *    KeyData.saltValue attribute and a blockKey byte array consisting of the following bytes: 0x5f,
             *    0xb2, 0xad, 0x01, 0x0c, 0xb9, 0xe1, and 0xf6 used to form an Initialization vector as specified in
             *    section 2.3.4.12. If the array of bytes is not an integral multiple of blockSize bytes, pad the
             *    array with 0x00 to the next integral multiple of blockSize bytes.
             * 4. Assign the encryptedHmacKey attribute to the base64-encoded form of the result of step 3.
             */
            byte[] vec = CryptoFunctions.GenerateIv(hashAlgo, header.KeySalt, kIntegrityKeyBlock, blockSize);
            Cipher cipher = CryptoFunctions.GetCipher(secretKey, cipherAlgo, ver.ChainingMode, vec, Cipher.DECRYPT_MODE);
            byte[] hmacKey = cipher.DoFinal(header.GetEncryptedHmacKey());
            hmacKey = CryptoFunctions.GetBlock0(hmacKey, hashAlgo.hashSize);

            /**
             * 5. Generate an HMAC, as specified in [RFC2104], of the encrypted form of the data (message),
             *    which the DataIntegrity element will verify by using the Salt generated in step 2 as the key.
             *    Note that the entire EncryptedPackage stream (1), including the StreamSize field, MUST be
             *    used as the message.
             * 6. Encrypt the HMAC as in step 3 by using a blockKey byte array consisting of the following bytes:
             *    0xa0, 0x67, 0x7f, 0x02, 0xb2, 0x2c, 0x84, and 0x33.
             * 7. Assign the encryptedHmacValue attribute to the base64-encoded form of the result of step 6.
             */
            vec = CryptoFunctions.GenerateIv(hashAlgo, header.KeySalt, kIntegrityValueBlock, blockSize);
            cipher = CryptoFunctions.GetCipher(secretKey, cipherAlgo, ver.ChainingMode, vec, Cipher.DECRYPT_MODE);
            byte[] hmacValue = cipher.DoFinal(header.GetEncryptedHmacValue());
            hmacValue = CryptoFunctions.GetBlock0(hmacValue, hashAlgo.hashSize);

            if (Arrays.Equals(verifierHashDec, verifierHash)) {
                SetSecretKey(secretKey);
                SetIntegrityHmacKey(hmacKey);
                SetIntegrityHmacValue(hmacValue);
                return true;
            } else {
                return false;
            }
        }

        /**
         * instead of a password, it's also possible to decrypt via certificate.
         * Warning: this code is experimental and hasn't been validated
         * 
         * @see <a href="http://social.msdn.microsoft.com/Forums/en-US/cc9092bb-0c82-4b5b-ae21-abf643bdb37c/agile-encryption-with-certificates">Agile encryption with certificates</a>
         *
         * @param keyPair
         * @param x509
         * @return true, when the data can be successfully decrypted with the given private key
         * @throws GeneralSecurityException
         */
        public bool VerifyPassword(KeyPair keyPair, X509Certificate x509) {
            AgileEncryptionVerifier ver = (AgileEncryptionVerifier)builder.GetVerifier();
            AgileEncryptionHeader header = (AgileEncryptionHeader)builder.GetHeader();
            HashAlgorithm hashAlgo = header.HashAlgorithm;
            CipherAlgorithm cipherAlgo = header.CipherAlgorithm;
            int blockSize = header.BlockSize;

            AgileEncryptionVerifier.AgileCertificateEntry ace = null;
            foreach (AgileEncryptionVerifier.AgileCertificateEntry aceEntry in ver.GetCertificates()) {
                if (x509.Equals(aceEntry.x509)) {
                    ace = aceEntry;
                    break;
                }
            }
            if (ace == null) return false;

            Cipher cipher = Cipher.GetInstance("RSA");
            cipher.Init(Cipher.DECRYPT_MODE, keyPair.getPrivate());
            byte[] keyspec = cipher.DoFinal(ace.encryptedKey);
            SecretKeySpec secretKey = new SecretKeySpec(keyspec, ver.CipherAlgorithm.jceId);

            CryptoFunctions.Mac x509Hmac = CryptoFunctions.GetMac(hashAlgo);
            x509Hmac.Init(secretKey);
            byte[] certVerifier = x509Hmac.DoFinal(ace.x509.GetEncoded());

            byte[] vec = CryptoFunctions.GenerateIv(hashAlgo, header.KeySalt, kIntegrityKeyBlock, blockSize);
            cipher = CryptoFunctions.GetCipher(secretKey, cipherAlgo, ver.ChainingMode, vec, Cipher.DECRYPT_MODE);
            byte[] hmacKey = cipher.DoFinal(header.GetEncryptedHmacKey());
            hmacKey = CryptoFunctions.GetBlock0(hmacKey, hashAlgo.hashSize);

            vec = CryptoFunctions.GenerateIv(hashAlgo, header.KeySalt, kIntegrityValueBlock, blockSize);
            cipher = CryptoFunctions.GetCipher(secretKey, cipherAlgo, ver.ChainingMode, vec, Cipher.DECRYPT_MODE);
            byte[] hmacValue = cipher.DoFinal(header.GetEncryptedHmacValue());
            hmacValue = CryptoFunctions.GetBlock0(hmacValue, hashAlgo.hashSize);


            if (Arrays.Equals(ace.certVerifier, certVerifier)) {
                SetSecretKey(secretKey);
                SetIntegrityHmacKey(hmacKey);
                SetIntegrityHmacValue(hmacValue);
                return true;
            } else {
                return false;
            }
        }

        protected internal static int GetNextBlockSize(int inputLen, int blockSize) {
            int FillSize;
            for (FillSize = blockSize; FillSize < inputLen; FillSize += blockSize) ;
            return FillSize;
        }

        protected internal static byte[] hashInput(IEncryptionInfoBuilder builder, byte[] pwHash, byte[] blockKey, byte[] inputKey, int cipherMode) {
            EncryptionVerifier ver = builder.GetVerifier();
            AgileDecryptor dec = (AgileDecryptor)builder.GetDecryptor();
            int keySize = dec.GetKeySizeInBytes();
            int blockSize = dec.GetBlockSizeInBytes();
            HashAlgorithm hashAlgo = ver.HashAlgorithm;
            byte[] salt = ver.Salt;

            byte[] intermedKey = CryptoFunctions.GenerateKey(pwHash, hashAlgo, blockKey, keySize);
            ISecretKey skey = new SecretKeySpec(intermedKey, ver.CipherAlgorithm.jceId);
            byte[] iv = CryptoFunctions.GenerateIv(hashAlgo, salt, null, blockSize);
            Cipher cipher = CryptoFunctions.GetCipher(skey, ver.CipherAlgorithm, ver.ChainingMode, iv, cipherMode);
            byte[] hashFinal;

            try {
                inputKey = CryptoFunctions.GetBlock0(inputKey, GetNextBlockSize(inputKey.Length, blockSize));
                hashFinal = cipher.DoFinal(inputKey);
                return hashFinal;
            } catch (Exception e) {
                throw new EncryptedDocumentException(e);
            }
        }

        public override InputStream GetDataStream(DirectoryNode dir) {
            DocumentInputStream dis = dir.CreateDocumentInputStream(DEFAULT_POIFS_ENTRY);
            _length = dis.ReadLong();

            ChunkedCipherInputStream cipherStream = new AgileCipherInputStream(dis, _length, builder, this);
            throw new NotImplementedException("AgileCipherInputStream should be derived from InputStream");
            //return cipherStream.GetStream();
        }

        public override long GetLength() {
            if (_length == -1) throw new InvalidOperationException("EcmaDecryptor.DataStream was not called");
            return _length;
        }


        protected internal static Cipher InitCipherForBlock(Cipher existing, int block, bool lastChunk, 
            IEncryptionInfoBuilder builder, ISecretKey skey, int encryptionMode)
        {
            EncryptionHeader header = builder.GetHeader();
            if (existing == null || lastChunk) {
                String pAdding = (lastChunk ? "PKCS5PAdding" : "NoPAdding");
                existing = CryptoFunctions.GetCipher(skey, header.CipherAlgorithm, header.ChainingMode, header.KeySalt, encryptionMode, pAdding);
            }

            byte[] blockKey = new byte[4];
            LittleEndian.PutInt(blockKey, 0, block);
            byte[] iv = CryptoFunctions.GenerateIv(header.HashAlgorithm, header.KeySalt, blockKey, header.BlockSize);

            AlgorithmParameterSpec aps;
            if (header.CipherAlgorithm == CipherAlgorithm.rc2)
            {
                aps = new RC2ParameterSpec(skey.GetEncoded().Length * 8, iv);
            }
            else
            {
                aps = new IvParameterSpec(iv);
            }

            existing.Init(encryptionMode, skey, aps);
            return existing;
        }

        /**
         * 2.3.4.15 Data Encryption (Agile Encryption)
         * 
         * The EncryptedPackage stream (1) MUST be encrypted in 4096-byte segments to facilitate nearly
         * random access while allowing CBC modes to be used in the encryption Process.
         * The Initialization vector for the encryption process MUST be obtained by using the zero-based
         * segment number as a blockKey and the binary form of the KeyData.saltValue as specified in
         * section 2.3.4.12. The block number MUST be represented as a 32-bit unsigned integer.
         * Data blocks MUST then be encrypted by using the Initialization vector and the intermediate key
         * obtained by decrypting the encryptedKeyValue from a KeyEncryptor Contained within the
         * KeyEncryptors sequence as specified in section 2.3.4.10. The data block MUST be pAdded to
         * the next integral multiple of the KeyData.blockSize value. Any pAdding bytes can be used. Note
         * that the StreamSize field of the EncryptedPackage field specifies the number of bytes of
         * unencrypted data as specified in section 2.3.4.4.
         */
        private class AgileCipherInputStream : ChunkedCipherInputStream {

            public AgileCipherInputStream(DocumentInputStream stream, long size,
                IEncryptionInfoBuilder builder, AgileDecryptor decryptor)
                    : base(stream, size, 4096, builder, decryptor)
            {
                this.builder = builder;
                this.decryptor = decryptor;
            }

            // TODO: calculate integrity hmac while Reading the stream
            // for a post-validation of the data

            protected override Cipher InitCipherForBlock(Cipher cipher, int block)
            {
                return AgileDecryptor.InitCipherForBlock(cipher, block, false, builder, decryptor.GetSecretKey(), Cipher.DECRYPT_MODE);
            }
        }
    }

}
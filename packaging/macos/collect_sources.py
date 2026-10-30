#!/usr/bin/env python3
"""Collect exact corresponding runtime sources as data, never execute them.

Pins come from the shipped rootfs APK database and signed APK controls. All
Alpine files are verified against the SHA512 values in their pinned APKBUILD;
recipes have their own reviewed SHA256 pins. Four downloads at most. Existing
verified files are reused; no runtime, identity, user settings or logs are read.
"""
import argparse
import base64
from concurrent.futures import ThreadPoolExecutor, as_completed
import hashlib
import gzip
import json
from pathlib import Path, PurePosixPath
import shutil
import tarfile
import urllib.error
import urllib.request
import uuid
import zipfile

ALPINE = [{'origin': 'alpine-baselayout',
  'commit': '540d8477019887cc6534152e92c3b12676e35d7b',
  'repo': 'main',
  'packages': [{'name': 'alpine-baselayout', 'version': '3.7.0-r0', 'license': 'GPL-2.0-only', 'base': True},
               {'name': 'alpine-baselayout-data', 'version': '3.7.0-r0', 'license': 'GPL-2.0-only', 'base': True}],
  'apkbuildSha256': '8909de40115e389d48f1d78315d9ffcbd2abc9c863b7d93782e78b51c6f0db00',
  'files': {'crontab': '6e169c0975a1ad1ad871a863e8ee83f053de9ad0b58d94952efa4c28a8c221445d9e9732ad8b52832a50919c2f39aa965a929b3d5b3f9e62f169e2b2e0813d82',
            'color_prompt.sh.disabled': '558071efdce2fe92afe4277006235b1a6368b070337c7567e5632a1a3fe531f87ca692eb36f3dda498d4d29d1f834fc8f7139f2985669ae3400b6d103d6f4c5e',
            '20locale.sh': '03361d912cf29c127608697ee14bfa5972f82a5c475e653378ca5f7670cbd8183efc7c8c339ff046ff6537944fe00c4a732bb6b552aecaecd1214ed3e11bdf90',
            'aliases.conf': 'bfe947bdd69e7d93b32c8cb4e2cabe5717cb6c1e1f49a74015ac2cfb13e96d1f12c4be23ae93a1d61aaa3760d33a032fa9bd99f227fb21223a76b5f5908acc65',
            'blacklist.conf': '0a1e1afa580751e80bf26057b65fadffe269c0552e7a1903de498f94973ba3da8453b51f25e649968ca5f4841266f5ccf951700fa28465a8614b83d07344de60',
            'i386.conf': '49109d434b577563849c43dd8141961ca798dada74d4d3f49003dac1911f522c43438b8241fa254e4faacdd90058f4d39a7d69b1f493f6d57422c1f706547c95',
            'group': 'b5eb01165c714861e860f17c0156911ff882a9010306b7fc4cdb22251acf8b1c91a3fa1d44cc41cb3d9b50892e2f98f43da57b002c5c33200c1bf49c3d2d587d',
            'inittab': '37d7b8348e604b12c055d9d7e79afb568ededea7153ff552c9f383cffd537d9c78cfd9facd612d2a6753fc626ff608a6d22d62637585a33166aa28f59fabed22',
            'passwd': 'f0d12f365839e7e262ec91e151119de7f2f253e9d0443157de4d52e183f421fbbb9eb0a83b9267a9ee850bebe41bff3c3cef553f9bda6e70d59a754a955be57d',
            'profile': '4eb857ed59c2edb257636d2bf196989e514a273e9701e9f076c9ae8c1589b4898269180569960acf072c0981ec7ea54014fd230f014401d6bb92314285d1e6aa',
            'protocols-6.4': '3a00083bcdf5a9e884c9d07877d52311e3d99e79cbee656e236ba06e08ba0dddb7ba76494fdc9dd1a826c48e197a790a69e6bb458e9df64832d6b5e904e9fd15',
            'services-6.4': '47b0f3ee73af2d259bd206a026204be0ea25531a895a0b035a904b38fe5407bc3dd2beab7f8fcb3d760587e6159702ebdb9cbc4f508942befdf7f10c10c87888'}},
 {'origin': 'alpine-keys',
  'commit': '6d473fb38effb2389f567b29fb7eb27039b3a279',
  'repo': 'main',
  'packages': [{'name': 'alpine-keys', 'version': '2.5-r0', 'license': 'MIT', 'base': True}],
  'apkbuildSha256': '8b51c80bff0f2477f9be7bc9ff98aa760f5759dfcd3a5026ca518c20a89347c6',
  'files': {'alpine-devel@lists.alpinelinux.org-58199dcc.rsa.pub': 'e4f9e314f8e506fba2cb3e599c6412a036ec37ce3a54990fc7d80a821d8728f40ee3b4aa8a15218d50341fa785d9ddf7c7471f45018c6a2065ab13664a1aa9e9',
            'alpine-devel@lists.alpinelinux.org-616ae350.rsa.pub': '51a5ec21283fe218809b2325202e1f8c9b2551705db48254b9d48a04f4ed0075de51e9886c4704647ffb309fd32d9850d14013848a53038039e85011251fe1cc',
            'alpine-devel@lists.alpinelinux.org-524d27bb.rsa.pub': '698fda502f70365a852de3c10636eadfc4f70a7a00f096581119aef665e248b787004ceef63f4c8cb18c6f88d18b8b1bd6b3c5d260e79e6d73a3cc09537b196e',
            'alpine-devel@lists.alpinelinux.org-616adfeb.rsa.pub': 'a98095a626f2dcbda73ffd8873ba2d609ee1d881f5da13b0eb3469ddd58b06440b4b0b2f791b037c88073e9a17c6dfc62dc1a4c8491bed871524d772ef04ad24',
            'alpine-devel@lists.alpinelinux.org-616a9724.rsa.pub': '7aa5526a88519ae91f997bf914a9bd3d230b21c011587f155ce22c4bb94b70181b28590027eb555d96d1122dffb8242c1fb044228e99b4e9b7650fcf6f5121c7',
            'alpine-devel@lists.alpinelinux.org-5243ef4b.rsa.pub': 'e18e65ee911eb1f8ea869f758e8f2c94cf2ac254ee7ab90a3de1d47b94a547c2066214abf710da21910ebedc0153d05fd4fe579cc5ce24f46e0cfd29a02b1a68',
            'alpine-devel@lists.alpinelinux.org-61666e3f.rsa.pub': 'b89d825e6af73687339848817791b294e2404162e2e069d9212d76d4ee53d6216eb75421a07b02f9778ef57dbb27962b2436247264eea1a1d882967ca0c18724',
            'alpine-devel@lists.alpinelinux.org-4a6a0840.rsa.pub': '2d4064cbe09ff958493ec86bcb925af9b7517825d1d9d8d00f2986201ad5952f986fea83d1e2c177e92130700bafa8c0bff61411b3cdb59a41e460ed719580a6',
            'alpine-devel@lists.alpinelinux.org-5261cecb.rsa.pub': '721134f289ab1e7dde9158359906017daee40983199fe55f28206c8cdc46b8fcf177a36f270ce374b0eba5dbe01f68cbb3e385ae78a54bb0a2ed1e83a4d820a5',
            'alpine-devel@lists.alpinelinux.org-6165ee59.rsa.pub': '8b9c2208c904c9f34d9d01d3d68b224208530e684265df214deb8c9e6b4b19633aa48a405e673249c9e93a8ee194a336e951cd82a4e27e5e66e85fdc5e0d495e',
            'alpine-devel@lists.alpinelinux.org-58cbb476.rsa.pub': 'bb5a3df8fac14a62d5936fb3722873fa6a121219b703cba955eb77de38c4384aeaf378fb9321a655e255f0be761e894e309b3789867279c1524dab6300cd8ef1',
            'alpine-devel@lists.alpinelinux.org-616abc23.rsa.pub': 'bad4da65221150a5d4cc6f63981e4dd203d40844d32e82c17f346eee5350e460e32d28f0e231a2b78d326ec32b898eec597d3787dae47dcacc9a9776d19fb4a1',
            'alpine-devel@lists.alpinelinux.org-58e4f17d.rsa.pub': '0666389ca53121453578cd4bef5fd06e159e291164b3e3233e7d6521604f8bebd30caeef1663adcd5309e07278833402c8a92c33294ec0c5cada24dc47c8cc98',
            'alpine-devel@lists.alpinelinux.org-616ac3bc.rsa.pub': '83fc29066f6073418ecf01176ce24c1c0e788508f3083a97691706e2c78323e53448060fb0d2abb8118a759570f1f0db9d39953c63fe26fe06da2be05dff393c',
            'alpine-devel@lists.alpinelinux.org-5e69ca50.rsa.pub': '66ce9677e9c2a7961d5d7bc5b162ed3114a7aef6d01181073c1f42a9934966eecded2ec09deb210f5a389d434d1641ba35fe3abdd5246b2e97d5a5b26a945c5c',
            'alpine-devel@lists.alpinelinux.org-60ac2099.rsa.pub': '34514100e502f449dcabe0aa550232c3330ed2f0b789b977eb228d4ac86afc93479474ac005914992a3b47c18ee3eb32ca27ccd0d392700a8f11f47d64a78969',
            'alpine-devel@lists.alpinelinux.org-616db30d.rsa.pub': '7cea57204a50d72bddff201c509ccbf06773d87062a3ead0a206cc6e4a00e0960f52d21f7cee7aaec6a4abba7a697e2e2e7f630fa1ccef7ee2c33908fca18998',
            'alpine-devel@lists.alpinelinux.org-66ba20fe.rsa.pub': '383ee8cdd7be983289ac7c31b3d6e8f11dd07008ec615503abad69ff69c96047434ec812a3778b6fe393ed59ff3ac3961a660b29a87febe85667c58493cb9e4b'}},
 {'origin': 'alpine-base',
  'commit': 'd5ce2bfde5b90c0ee710a4bd0323a74671b2983b',
  'repo': 'main',
  'packages': [{'name': 'alpine-release', 'version': '3.22.7-r0', 'license': 'MIT', 'base': True}],
  'apkbuildSha256': 'c1bac37cc9fa4af594851f881d43e20533b443b00c281031a32dd0eab14c294d',
  'files': {}},
 {'origin': 'apk-tools',
  'commit': 'a53adea12086d36717300ea195ede9ad75f98e1f',
  'repo': 'main',
  'packages': [{'name': 'apk-tools', 'version': '2.14.12-r0', 'license': 'GPL-2.0-only', 'base': True},
               {'name': 'libapk2', 'version': '2.14.12-r0', 'license': 'GPL-2.0-only', 'base': True}],
  'apkbuildSha256': '86783fcba97fcfdb2fbcc36d984e0a70e80cae25f9d0a4c7474c2c9a4bb4c6fd',
  'files': {'apk-tools-v2.14.12.tar.gz': 'd319980f7d57246ad885202736bb20dd88f031f7e8b058f612fd5df1a5e88387522f599b5a03ab4507b51385675cadd67c6fcf50817ce6dcfdd410d2be5aa5fc',
            '_apk': '92d5ee1edaae8e9bf32938e6b901e2a6f97530fcb6493060acfbb8883bf8bc825462f2e8ce6a6cfeba43acb4dd6d36acd33d060a4bce3907562e60c7cdb65e5f'}},
 {'origin': 'busybox',
  'commit': '2e97d754a30d558f15524b8e422303c8a96832df',
  'repo': 'main',
  'packages': [{'name': 'busybox', 'version': '1.37.0-r20', 'license': 'GPL-2.0-only', 'base': True},
               {'name': 'busybox-binsh', 'version': '1.37.0-r20', 'license': 'GPL-2.0-only', 'base': True},
               {'name': 'ssl_client', 'version': '1.37.0-r20', 'license': 'GPL-2.0-only', 'base': True}],
  'apkbuildSha256': '3bcc81f5784fbe2e72624fd58015c46df6ee8b8ea0cc731bfae53ffbf1cfca9f',
  'files': {'busybox-1.37.0.tar.bz2': 'ad8fd06f082699774f990a53d7a73b189ed404fe0a2166aff13eae4d9d8ee5c9239493befe949c98801fe7897520dbff3ed0224faa7205854ce4fa975e18467e',
            '0001-wget-add-header-Accept.patch': 'b148e37fc23d65ac011fa85142fcd645a55171593e889a86743b1557d9be05b6c39e8d0a798a536dfa29d17f71d6fb0b422f031128415b07ddec3c209ce93814',
            '0002-adduser-default-to-sbin-nologin-as-shell-for-system-.patch': 'c725043253cae924b989cfba739d024b172b20a300de63acd26d4ec0624a13b5ea4b07f53cf991c4991cb611454c70ebe5bdb8e1a8e62d5d5d7e2e3823bed2bf',
            '0003-ash-add-built-in-BB_ASH_VERSION-variable.patch': 'e9ba4f56bd0841f6b998f79aaf73a09c4096fec0f817594be294c12a03ba712f1103fb8b0716fcf2fb04f5dc9efa04aa6dc9b61150b3df00b302ab6f9608d313',
            '0004-Avoid-redefined-warnings-when-buiding-with-utmps.patch': 'c6acd1a4cbbc8b7b6c66b20c4cb958c12e887a43edbea35c0c4db3b4139afc5b4246058f503a5aaac0d05badffee93e6bf91111ad0cc856d9f5074baa7194df8',
            '0005-libbb-sockaddr2str-ensure-only-printable-characters-.patch': '0c4eba3a6690b4989ced7f40a2f2369fe8ca97caa226838bde6412031290722d380a27203c67f8a2bfb3319590c67a0a108f8d8a04cff9add54085bdea520c25',
            '0006-modinfo-add-k-option-for-kernel-version.patch': '68637265f28f73e1538045bb23f6a585248a73d60eb81b1fb75a67e9946b9f3928ebb6d8794976f724a6f0e0324748e3eafbf1dff5e1c78254200cb3acbc51cd',
            '0007-nologin-Install-applet-to-sbin-instead-of-usr-sbin.patch': '6a0714d051dc29e39d62271c317455bb7d1b28117f60cdc3e887dddbb38df28ae5c17da8033b4250c1bed76f1e8e29b2c013e87f9d547a5bdf75c4b9433fc2bd',
            '0008-pgrep-add-support-for-matching-against-UID-and-RUID.patch': '220da5e908ae3c6f836faf8549f5c610e74a76563103152f9d20e2b2ca1663ba7baf803067286684753a2c737427c5fcf317d7da60293412f601b4ca17e77e00',
            '0009-properly-fix-wget-https-support.patch': 'b4697696c2c44f5c2c2be167774784b87f45d7d1433db8ed040ae37a99d01275a2a0b79db7e6314636f76db0f7b69288f1f7d2fb96c097929524d49832ea0913',
            '0010-fsck-resolve-LABEL-.-UUID-.-spec-to-device.patch': '16e490902496cfe199551432dd3cce480d20d79b98056b6fd5086a5b5a16ace0cda8357d8fb7bdc3e758273c3ec91e416f926c3e54dbeb54136c6853ce23bc7e',
            '0011-nslookup-sanitize-all-printed-strings-with-printable.patch': '6671165e7dc3546b42edc7cf3304ddb755c707a7c7976bc544f7da6ad974c09bfa5e7cc2e83a98ece8e919808ae73199b19930d390c8ac34527f58f28ba836b6',
            '0012-ash-exec-busybox.static.patch': 'f2a44cf6903d6ecac3bd226b2abb2fbd692f883dc0d296df0a8d2c34350673c51f18d68bc9819d7badbab0541839f4e91361d85f014159af8e0fab82e324f84e',
            '0013-app-location-for-cpio-vi-and-lspci.patch': '9788eed96fc490ea393e760657b8d8d7a09696e24515e30099dffe8c3d534a6018ca2cf17e689e8740438b2fdcc0c3ed67dc841e6c03f39322d12b60edc66a97',
            '0014-udhcpc-set-default-discover-retries-to-5.patch': '52094fb50723d924feeb31b86d8b71143505b8d66f6b881c43859c1a346d5a52ef8fb5aa7806f1f1892cbd083b03fc294d1181761eef01ffb564717e121a2300',
            '0015-ping-make-ping-work-without-root-privileges.patch': 'dae2f3a137edcbf31e0afb8565f372a549ba67997c391e755a9f29b4c986219a9516b1e1be589fddf81f136d6c7700c91fb83c46aed74c018a58ab06ea2d0567',
            '0016-fbsplash-support-console-switching.patch': '6ede756c01ce7fdcd02a38188a86f4cacff4066eb5032bbb716edf8d4eb3e437de767f04cd0ed8a3e915ee10270465be59248f72d2e7a15abc3d865feba0a361',
            '0017-fbsplash-support-image-and-bar-alignment-and-positio.patch': '85209a0fb5368dfbb831095d381ec135313c26030f3eb4c5df8a40051fa6932e6b49a91965ea745b670efbc7a8e32707a08bfde36c923912f5e67fe2d6bbb1a5',
            '0018-depmod-support-generating-kmod-binary-index-files.patch': '67a21b4754534b5faceee3da1c24e037e8e259924fd415cdfd5aabfff6c2bea3d15d58cd32c29359bf5b311e666ad1ff01a1ec043be1051ba359c52921449c18',
            '0019-Add-flag-for-not-following-symlinks-when-recursing.patch': 'b74cb8d9c635432f7a2205a91debf023cc4e5fcf5cf8b25e690711afaf24956f772aa70782f266a9b997e16069b828f993a015e93248842808a721e00a8530b7',
            '0020-udhcpc-Don-t-background-if-n-is-given.patch': '95b82ac5cd0367c15008dd3568ffb68f6489065618fe3f5d04c30650a5492d1314374a3d83b5aeea078b003b43c1b856a0a1b52434fdfe0e3eb299fd44d16a59',
            '0021-tests-fix-tarball-creation.patch': '65a62f6013d2ee55e0ba32836adf48fd572fe7f931b50bb56bf778673a5ea6883d85c08bda0df4b87f5181433a3663bd15d805f75cdef3974e355a05f5b13c70',
            '0022-tests-musl-doesn-t-seem-to-recognize-UTC0-as-a-timez.patch': '99f3469a057040b38f4a66053109fb874bac7b4ec0481c515fe675c24ed8f99a89b756fce977ad122edff43aedf722ebdd7e7e475adb5edbae207fdcc4429b5d',
            '0023-Hackfix-to-disable-HW-acceleration-for-MD5-SHA1-on-x.patch': 'd82c99af216d8f4a90cf7dae78ca73ad348fa1c70cd50a65376a768a6426e87071e1036cc9c3ab3606986cc99886b4a4f53685d4f26f3aec20c0cb07df5a936a',
            '0024-umount-Implement-O-option-to-unmount-by-mount-option.patch': 'a67640383d0b02c1a1eb177543270776922c09bdf8ac442d5dfa16ba63b99cf31f105b8db4f5ca21413be1057e55cc4a42314143bea505027d3a36e7e7e786a9',
            '0025-find-fix-xdev-depth-and-delete.patch': 'e6aaa90843082ee01d4a0e14756399bbed386e6ff30ed853bb1420bc351a8a15b773be60d5f86f9a3ddbe5106e08e2b185b44e64a6ef210eb26b270dc32b211f',
            '0026-awk.c-fix-CVE-2023-42366-bug-15874.patch': 'e2c6367162b886654d64a1988e383c05e2792183265c3542d68e535daa5a9509c3e6a42b9aad0367a872fd494089a7b3911e8a438c47db16fbf21b656b030382',
            '0027-awk-Mark-test-for-handling-of-start-of-word-pattern-.patch': '82b203472b8cdb790c15dbdb5374de45dbe2fcf926c16910b890540278d4ff6bb843fed3a8840a0eb6d340e0eac27ced0c2a4103d37848b51c61e94a3f0436da',
            '0028-od-Skip-od-B-on-big-endian.patch': '711c92902f4d99863c0433e0670999a456c588cf8a1a4729678244d61607aa82269c1944942617650902ce242fbfe3f9d68593ae0a21e4c17bfc04470f4253ba',
            '0029-ash-reject-unknown-long-options.patch': 'd9625580bdfe7036e0a3e01c33ddffcc8c70737dfa031c9f97a5a45635910dbd5cf3ebb8d28256a10ee7a13efa1e396a93404099db7a5143adf924a953d4cbe7',
            '0030-hexdump-Skip-a-single-test-on-big-endian-systems.patch': '3b9542d3ec24d9d3fd12b69f60ca1014b49240b92d8517e4467f62b0e266102a73cdac784766a5548a909e08a497c577b1e880604d1859eee18ac455d8bc56b2',
            '0031-syslogd-fix-wrong-OPT_locallog-flag-detection.patch': '4c2b7d7c623b318087f20b0bef9140cfa3f83111bfc922bba50ddd16edb8844c848c8b93c5d31303b7f253673fbff8ccf3d4ab4b9dad52fde3e04744b0782170',
            '0032-lineedit-fix-some-tab-completions-written-to-stdout.patch': 'ac56a170a31f4329d00155a2a7d3950c4fe365e69e2e6b773a28fe3faa214a1330f0dd910bf0712453f033abc9e8465f9cec80f4e3e820a5d6ffafbf36e503e7',
            '0033-lineedit-use-stdout-for-shell-history-builtin.patch': '82c328921c4034b561f31fe5da77fd1a03033911bb6f50383b59885b184e010bbf66065ce0d90e0bc138a7608a4a5b51cab5d3598f974ee600d06eb161d5b0c3',
            '0034-adduser-remove-preconfigured-GECOS-full-name-field.patch': '04ed80099092bf6cafa293d13039184307197d5b71e2052a7b5650eed4364845cca3910e701e01c67a23dff7c9552506b682d553d6bdd7dfcbc7724e98595c4d',
            '0001-hexdump-fix-regression-with-n4-e-u.patch': 'ac026f8b289d1431c46d51a8c512eb0184aa81de1c41971a87c99bcdc5c63c2d9abed412cad20d3032b480d87a52ee2602411149eb5f271fa9d051165a7b8188',
            '0001-awk-fix-handling-of-literal-backslashes-in-replaceme.patch': '7165661f3972f1800faf8d263126e3f238bb26bb7891230dbaad2d085e49e2ababc555294ecefed9d8ff00d93c8c3963e786db796cf3d03d68bf9e0492305d0c',
            '0001-tunctl-fix-segfault-on-ioctl-failure.patch': '9548603cbadf690ab31354642ab3f7bef1b9f696df6b661ea644548fb831b2f78a47a9b09b5489aa43da7061dcc6808864d130d053906f44090983f5cb3dc169',
            '0001-init-add-support-for-separate-reboot-action.patch': '109d3e6a0141b0b28352c299a7d520f39d4626d424566d6ec14df95ba27f0ae8a755bb275c73217c07745c7b7f6fc7eb71555db818536afb09f04f29c4cd8b2f',
            '0001-tar-fix-TOCTOU-symlink-race-condition.patch': '906d6c9ff1a9b911997f8ca9debfc07c48c7f5ac8eac51860b076e8483979ea175028f7207cbe878769e58213e8293ada155b03c9691077ce2f6a91820ab319a',
            '0001-netstat-sanitize-process-names.patch': 'bc6dcd9651f02da3d848e8c848b536fe412311f7bdc66ff65912a1aa175c96c8d5336e5a4e21f8a73b76438c9d1bae444736994ec460d5645917fa46273fff88',
            '0001-tar-fix-16018-masking-of-potentially-malicious-tar-cpio-content.patch': 'fa200c556a8b79506aa398e0d81eb2690726860ab15e48ba80c1680abab2a48e9093bf3c5a67dc5d8eef90a3e15e5a873a9eef1047cef0c031b233f8a211ee68',
            'acpid.logrotate': 'aa93095e20de88730f526c6f463cef711b290b9582cdbd8c1ba2bd290019150cbeaa7007c2e15f0362d5b9315dd63f60511878f0ea05e893f4fdfb4a54af3fb1',
            'busyboxconfig': '93f0fa512a394a07f760b13bd70a28424d3ad1a6c283b77fd4894114c5b472cfbe65f3441afa28596771d75527aaa9794706a3b329b4898e5d83bce799169929',
            'busyboxconfig-extras': '2f6efba02078647137f0518284c60004bf28d29a77146d488f813c4db6971ccf6ec5157734c521516e6f3aa570c804fe3ed2c69c46a3ef8636c5f88758948eb7',
            'bbsuid.c': '0becc2186d6c32fb0c401cf7bc0e46268b38ce8892db33be1daf40273024c1c02d518283f44086a313a2ccef34230a1d945ec148cc173f26e6aa9d88a7426e54',
            'dad.if-up': 'afc27da5f95bec3e3ccb5b1fcb5bd80a8317d8753e6c70cc0750d0875313e44bf2e487472c8499d7bbe5afba3e583c75548ccdcff4dcadc94bb791f53fb77ba3',
            'ssl_client.c': '1c7c78afb274d1316725b22585452efd7de9a80ad326f099f25e34d0877c5d81ced579e2dc795314221e316f58071374f61ee22c6ffd34072266525f65d3614c',
            'default.script': 'aa70f33eee30d9d8348104a009bb12c6853146343bdf01f7e119c0de892d379deb05be9a4aa6bf9fa67dbb3fda04f82c5c7a32cb886b3cf6ee9fbaf699789b26',
            'udhcpc.conf': '5f9ff70f99a970e88460c0de81a6637d8a82252fba6715e259833f16ed8cbfc3440972b1fa454eb77dbbcd1e244774c96b4eef96f71a2b67a9039a0dd5e6fc1e',
            'securetty': '38d6ce0dfff65ce7ec644f5d7aca70de943f4d5fb8820c772f5d945926b678de280f19831d3f3d0dc0dcda16812841748b57eaa745b784a5e216a59657afbd71',
            'acpid-poweroff.sh': '95187d7f4f91a1be353d9c5922238da6ff1f688a1a7366678c10163c683ba7fd82b3fecc71e86e04197aa322322e0152a3cf86b4ceca19dd04e9a9c763fede2f',
            'acpid.initd': 'fcb532233fd7ba8cad302d037b88cff00ee8b96b37c90b34fc823479208cf7cdda48818c972ce2c4703b7283fd58e99ba8a724818f884f3b09eaa7e2d6ffad21',
            'crond.confd': '34c6f3197064bb91619b899b28a201bd4d920b18bded3845440b2cb36dc6f16cabf447c96878349b16e46c30184cbe48bac00a01c5f7cf1be038c0b7136064c5',
            'crond.initd': '0e8266b1bf1d533de7531f32b27c815db00378df6e9ee98232ba3b9cb4f3e1770db65290d338ac0745aca52dd99c3c128dde3fa81ed110dd02c4a298a2f62b1b',
            'klogd.confd': 'f9bf43b72142bbb4c0665c1268a3d91586ef8a057dfe64c53b6923815d2db1f669b8080a38311ad89cda0b783a8628700fc5414d834a21d8e48515760bb910f7',
            'klogd.initd': '50b975ac94722e8584249a73b7f36134cb954ad19716b7f3437a6e2cde2a81465ef153b04a0b8e734fc7971320df71cd8c75e669bb4da86233c1991bc516260f',
            'ntpd.confd': 'b0ba67585f39d83320ed6de183d7463a0e163807b9f3dc6f0baa212236056f22aeb2c5399dfdcd54929eb5ae06a15714c2fed133b5888869ed9d9cf589cef843',
            'ntpd.initd': '49d3b84e7f3fde1fe4f8d38e74fa175c55e6edd1c82e3e41c5ea368abdeea359207c984c644550bc6f96949e09cd4a8da6a473c495287a163a0248a9bb452411',
            'rdate.confd': '11b2a71c38c87d95cd19307481b5f05d9aa980f1ee8348a90b14d698a78b5d0109974fef782ae6538177bcf8bb9fc9cd6f8e4a368ff6b6266276745f1394c2e0',
            'rdate.initd': 'a95d03564712803fc58c48534f2fbbf23dd8418ffd3a6a06f840e969627892eea518b4fe17f059850665fc056cedf972c26aee52c309805987fb5c59a710bd30',
            'syslog.confd': 'bf8173ee33a5d2b61cbdbc1b070e599a17a21e5433a0d8aa9beef71e4ac831304076af8e7e58dc594cdee61562329793afdc57af91e5496bf5fffb9b734a3d9c',
            'syslog.initd': 'a13a6add6f7fb10a3a2563391b6f8b68161249147e9f50e6d857c5689123f6d974368ce4f13b8d93312fa38d05f604d67c4c976d8a7eef301af50a3a6ad50c23',
            'loadkmap.confd': 'acbba3385fb2c416362eb9176f24146b617a2e394ee0e00b9eff89350f82468a5ab8396f4f52b0aaf03d341ac6150c73182e808fa3f29a2dc71c69271cdfb811',
            'loadkmap.initd': '1b61a0f1eb40f2ea14bf3ed3441996f343d70fd45858d443f21244f133dcdf1c64433c3deaef769eee4e3e698b405f25e1bbc185a25c351a88ce7d120d61cf00',
            'watchdog.confd': '359af0a4a1841dd6557eaf3e71d39f0d7a2687bad6a8fc6ef436eccf72a40f85c6400a3b7880c7f64d8e460b6af8a0ff3dd5ffc3a4b1aa8eb0d8f7eaf99ee3e4',
            'watchdog.initd': '53d19009e571dcd3920890c88da815709bfadfffc6946e2dec347d80291622475381a61e9b5cccc15f69f15c0778c192c0112a9f70f379205490b1924ad44a20',
            'mdev.initd': '6ce0b2a8fe69cc7ea657c5b9076aba51c8f0beeaafa4a887d8673bcc9f9cf8ee40f4b07d2d901ec7a1a1e4f29c150c496559559e803595d0bd487dec56b530a2',
            'dnsd.initd': '8af4a5652c418274c3f9ea2698d5026ebea78cd6cbd78e7779125d039aa5807fe92a44e70062b53fe6b830c3fe479f0e6d6ee4a61f37cf4814e895cf1b6ea215',
            'httpd.initd': 'b8ffad8394dee7c2613a047cc34d6b7d650b576c6c9c8427c1ecf9148ca57e4cb3ea9fbc13525f104dde8e30bc78fe3ad4747d534b55750f5c76e8f98368cd33',
            'inetd.initd': '7b286eaba66a4c86366eec38cd94d8547a15bff72923456edf8c683d4e02bf55d85628adbdb7c1c44f3b16b545ea0ca238afa14a73fc1ade0c33ed7e9fcf2b49',
            'udhcpd.initd': '4a8d1e924284fca730a262fedc3cb76b4f4689a6c650b0c5544b65eeab6cfd9e8eb91f448d9ebbba6efbb056695428c1b26e5eaeb7233bae090676d1789954cc'}},
 {'origin': 'ca-certificates',
  'commit': 'ce7bff87478cd936f5b7031ce822b02d4fdd313a',
  'repo': 'main',
  'packages': [{'name': 'ca-certificates-bundle',
                'version': '20261020-r0',
                'license': 'MPL-2.0 AND MIT',
                'base': True}],
  'apkbuildSha256': '7b35fb8e11790105578f47d57a3b22831227b0d8ad7d064e575bad146023f3da',
  'files': {'ca-certificates-20261020.tar.bz2': 'd28eb01751b1a914ffb5d8cb99e662f0a35a95148c21dcf98de1ab3108b54db9714a99472d07715023867f4f76a21f2a79a2bc1c7e9876ce5167fdd865ad2dd7'}},
 {'origin': 'openssl',
  'commit': 'b6d0d5c5639c0034e9073c39e9ff139e28e71818',
  'repo': 'main',
  'packages': [{'name': 'libcrypto3', 'version': '3.5.9-r0', 'license': 'Apache-2.0', 'base': True},
               {'name': 'libssl3', 'version': '3.5.9-r0', 'license': 'Apache-2.0', 'base': True}],
  'apkbuildSha256': '043006cc083576fd4c2869af6b3bce0de19f1509bab35c0b31e14fc9accda090',
  'files': {'openssl-3.5.9.tar.gz': 'c4c8136fd2c98d13bb85a57bfb0def25c906f47f1fe97675cd114594944eab957b5b81a3e0f48826e1a0f093afff5e83766b539d49768f6db68310fd678d7de6',
            'auxv.patch': 'b2541075148fd5af4552d34158deb1a325f5adced90626dc03fd126f47323cde949b15b2657523c34b26530b322312a43bd42ada7295b4f2dd1d3b5d11892c62'}},
 {'origin': 'musl',
  'commit': 'ddaf59b84bc6983a37d6e24d2210c2c4791642bb',
  'repo': 'main',
  'packages': [{'name': 'musl', 'version': '1.2.5-r13', 'license': 'MIT', 'base': True},
               {'name': 'musl-utils',
                'version': '1.2.5-r13',
                'license': 'MIT AND BSD-2-Clause AND GPL-2.0-or-later',
                'base': True}],
  'apkbuildSha256': 'e457fe06cb2efe6a60d25540a64622186037dd508533c0db9f0017700ed6df4a',
  'files': {'musl-1.2.5.tar.gz': '7bb7f7833923cd69c7a1a9b8a5f1784bfd5289663eb6061dcd43d583e45987df8a68a1be05d75cc1c88a3f5b610653d1a70f4a9cff4d8f7fd41ae73ee058c17c',
            'handle-aux-at_base.patch': 'a76f79b801497ad994746cf82bb6eaf86f9e1ae646e6819fbae8532a7f4eee53a96ac1d4e789ec8f66aea2a68027b0597f7a579b3369e01258da8accfce41370',
            'isatty-glibc-align.patch': '4fe4309d9e5eb12f3aaf9dac300c7bf1a9e104d9b0e7754353314f4b6507de68d8d5bf0ac45164d7e2ab4d71728b5ef8b302f8ee7a6b548f54e022218031e2cf',
            'ldso-fix-non-functional-fix-to-early-dynamic-PAGE_SI.patch': '1b85079c706b017106ecbf4afbc65b4a2171297a673035645e4c3ddb7e2aae0549b04932df83778323b7329ae8d32160a89e0c1b2534c278708c61b1f40a1ed8',
            'loongarch64-Add-lsx-and-lasx-vector-structure-defini.patch': 'b4d95231944fd236827a62343bbce74b20a56739e2b8937bda69bfc7b53f2ce5bd709459ee61615bd10d2839f43311eced9344ee8185701fd38ec0de0d43c6ae',
            'loongarch64-add-TLSDESC-support.patch': '363d0cbfe11f024b120b6d54e909c038cd5b37f1f195048ddb29591191a1288ff50842ccd14e0cab80359e5b46c0e02d2b5af3016bd1507c1d269e590690a3fe',
            'loongarch64-add-fpu-and-simd-context-to-signal.h.patch': 'b0f93f8868caf15d436ebe81088f4914cac3e21cc5b86cb0d5f40092edc6e29909188a14cde0c69ab86b51631aa34a4b8136a515f2a1b53c424c8b251c5ea689',
            'loongarch64-add-bits-hwcap.h.patch': '70003d89e31b1c83274d1e850f86d8a54f30c1d5e22f769ae229d30d2bbdc095dbf4c262fb55079aaf8bfdf9a213f9468150b50c000b6596b051729a0d25f913',
            'LoongArch64-add-new-reloc-types-and-NT_LOONGARCH_HW_.patch': '546fa317b532c3a63a592b6613e9a2136ee7750113ab048b261acf94e33a34732f3f26d6f734879c025d944f0b8e5307656c485bd32ce8f2364ee537ab236c95',
            'CVE-2025-26519.patch': '7a6a9836d2de91afc1115868e68f347bd2365fa48188e65938cfa654ae9bafdbb3a56bf12d3185a96800a85198378c8dbf9c25d977ca0e318220529fa4458123',
            'CVE-2026-6042.patch': 'f7849abeab0e4eab992a80464afa07b9c8aae5cee76040523b9dbc0931435f28ea8f5792b8a4a0cd6d608ac2f37e3225e89afcb3171a0a6df44450ed57cee83b',
            'CVE-2026-40200.patch': 'a64ab7688d1a85e560b5687783df482d2467a79a74400da5a1601382847d6b4e6a79b7529a8dc80c46ddda8dc2a812f2261557fc8ee98dc3bdf7322761bd6d9c',
            'CVE-2026-107891.patch': 'df73a35b9da531021ad5b1a6aad12b9d8d69de2bed7fc91b1156d9727476ab25c0d3c2341d60488152ee8ccbddb83786e91fff950cdb177b9b04673da98fdc4e',
            'ldconfig': '8d3a2d5315fc56fee7da9abb8b89bb38c6046c33d154c10d168fb35bfde6b0cf9f13042a3bceee34daf091bc409d699223735dcf19f382eeee1f6be34154f26f',
            '__stack_chk_fail_local.c': '062bb49fa54839010acd4af113e20f7263dde1c8a2ca359b5fb2661ef9ed9d84a0f7c3bc10c25dcfa10bb3c5a4874588dff636ac43d5dbb3d748d75400756d0b',
            'getconf.c': '0d80f37b34a35e3d14b012257c50862dfeb9d2c81139ea2dfa101d981d093b009b9fa450ba27a708ac59377a48626971dfc58e20a3799084a65777a0c32cbc7d',
            'getent.c': '27fa820dc847b1e4a89a45ab7c7ffb8b6436b6b7f1089004b12561d939b227617797d5fc81696658da3f07daab6cc71ea171eb6afb6f5082336b57e2d0ac8463',
            'iconv.c': '9d42d66fb1facce2b85dad919be5be819ee290bd26ca2db00982b2f8e055a0196290a008711cbe2b18ec9eee8d2270e3b3a4692c5a1b807013baa5c2b70a2bbf'}},
 {'origin': 'pax-utils',
  'commit': '9818c555f344e3085d0dd1f3d917d8e23e244cb1',
  'repo': 'main',
  'packages': [{'name': 'scanelf', 'version': '1.3.8-r1', 'license': 'GPL-2.0-only', 'base': True}],
  'apkbuildSha256': '13021bb4f3e1026b35a1f789b7222645eaeb99cc8ae109b4ce0a60e7156e1795',
  'files': {'pax-utils-1.3.8.tar.xz': '0dde95f86802729d80b7b38af84dec636e973f6abc70600633edcb05d3d5f95c1b2861300ce478dd7f798c7a1e5eccb1011c06c53adba38e11a996b69d463656'}},
 {'origin': 'zlib',
  'commit': '121fc97db0e7c59db704891b6d7ef83ae5df2cc2',
  'repo': 'main',
  'packages': [{'name': 'zlib', 'version': '1.3.2-r1', 'license': 'Zlib', 'base': True}],
  'apkbuildSha256': '6a3c1cfd5f3670a84b1ad522d0007be2282b4fa2441b29273a12d32a86693ec3',
  'files': {'zlib-1.3.2.tar.gz': '70963771ea5d763614278a69b474f09b7d237ef8f53b675a10fe31d9923aeef601504b35d7ebd1b1e7f347e9ebb048e6b3b47fffdf137e7bdc7e8d5eb4ec4692',
            'CVE-2026-85091.patch': '878888308a51296470f05eca59a79212314d4c9a26c19bbf70c1cb1b8d25eb42e0cce5a001acbe287268aa215c854cb5a9990ff62b7aefa9de221803d172957e'}},
 {'origin': 'eudev',
  'commit': '8bac74716d06c84627ab02d101fd9d2bafd0a34c',
  'repo': 'main',
  'packages': [{'name': 'eudev-libs', 'version': '3.2.14-r5', 'license': 'GPL-2.0-or-later', 'base': False}],
  'apkbuildSha256': '03bbb8b5034f747087c32645680ea6fac16e3eed8cd76d47f7ab1a60ea9751f3',
  'files': {'eudev-3.2.14.tar.gz': '1e7593b53c5508a99fe7ff5d24d1d09b3753d9cfba086e7d8cc6ab80cc88013ec687dc4f1eac7b7889051f4e5fd5c40d2b87f5f2d948479d679c8836014d08e0',
            'default-rules.patch': '683e3c26ca4f058303f8db7d4977e436d728386ee6866719c4f11ff31db27572d3f486f51d3173310e1ec2cf995fa4c396f2415bdf45dabdd80e6e55e798a314',
            'load-fbcon.patch': 'ff5928fd555e095d9f3234dd004ac4c5925405d308777e9b018e8e03112cd109935b51d75a3bc4a2a1018eb486e8a5e5ef6ee978860002a8fff93b116e9721f5',
            'no-sgx.patch': '1558800e4132ed2c10b37805fca044ff3129cdfebec71d3e758143fab9ffda824e86d52960926a9042b21fc22f0cda8fe6690484913bd7c3dc4436af92923efe',
            '78-sound-card.rules_Import_ID_PATH_early.patch': '8872f124267485d682dea9c7c47aaa8c228aadef90981c430af7ca7f7025b344d2c3c07c653241190a3f44096aa1d3aff990dd05210f5b278d999d6fede659b5',
            'udev-postmount.initd': '8ef1b911843ab13acb1c1b9b7a0a5cd76659f395c3db9e579429556f23eacebb414507dc0231e2455e7589bc70054fa1e6b6dd93dd833f7101c0da0597aabf88'}},
 {'origin': 'hwdata',
  'commit': '384053bb3c4e39ea7b97de07cabfdf6b72cd26ae',
  'repo': 'main',
  'packages': [{'name': 'hwdata-usb',
                'version': '0.395-r0',
                'license': 'GPL-2.0-or-later OR XFree86-1.1',
                'base': False}],
  'apkbuildSha256': 'cfe0aa3ec64616a943c525ed8682fd91e24fca0d73851bbb45736f17d7a5bf6c',
  'files': {'hwdata-0.395.tar.gz': '3ad259200a6ecad3f9e2e05a59216494b56880f78b7749026a41f099a02a942917c11532540fcd5b7c74fe94d95f1665e3933ef580e048ccc87cafb646d2dd7a'}},
 {'origin': 'ncurses',
  'commit': '3fefbe8dda8b3e85458edb190e2f3a69ad16035c',
  'repo': 'main',
  'packages': [{'name': 'libncursesw', 'version': '6.5_p20250503-r0', 'license': 'X11', 'base': False},
               {'name': 'ncurses-terminfo-base', 'version': '6.5_p20250503-r0', 'license': 'X11', 'base': False}],
  'apkbuildSha256': 'c590c647126b1f91a2fd95842b6c727379f608460a2b6fcb2047155546dbcf4c',
  'files': {'ncurses-6.5-20250503.tgz': 'b417ea609e497eb746ddb3bef89eafb9eb9e78e605b4b8445cc47f005c22dae24c2c461cd014f7a76c1c6e3f685e0363a7a229af24360b4505483e3bd36efd41',
            'cleanup-pkgconfig-ldflags.patch': '201ef1876655101cedabc83a0ce46f75079b08f565ca8de4cf96fd69e41332a2d0597b77fe360dc58b10772586fa39bd52ac9ee670a912fef84840278356065a'}},
 {'origin': 'libusb',
  'commit': '0ad4e679129e2210dd3376908eca629bdb046266',
  'repo': 'main',
  'packages': [{'name': 'libusb', 'version': '1.0.28-r0', 'license': 'LGPL-2.1-or-later', 'base': False}],
  'apkbuildSha256': '3f6839a2d6230b84cb0f11217721d98e58eb4935a301b8dd0160598c18ca4f7c',
  'files': {'libusb-1.0.28.tar.bz2': '0f4efa6b54e6195d2e5446652c2dc07358583e205d63bf438c4409511b8637d1700a71268c40499755747827d23cc730d9122267386f847bf781993c045c519f'}},
 {'origin': 'linux-lts',
  'commit': '6a0c60d347a1193d1d7ebb41a6e72dc0da1e8137',
  'repo': 'main',
  'packages': [{'name': 'linux-lts', 'version': '6.12.114-r0', 'license': 'GPL-2.0-only', 'base': False}],
  'apkbuildSha256': 'db26ec40522fcb5627382d1da9447474b4f737ee4ba8597e9556b9bf73c43f80',
  'files': {'patch-6.12.114.patch.xz': 'e9e101066d5903322db14106aaab0d965586d43d1cde6af8595bf2209f49fd40917799afc686c5afde86edaf4c4c2660d9123d6eb717c1b13474d3d2c099f1c7',
            'linux-6.12.tar.xz': 'a37b1823df7b4f72542f689b65882634740ba0401a42fdcf6601d9efd2e132e5a7650e70450ba76f6cd1f13ca31180f2ccee9d54fe4df89bc0000ade4380a548',
            '0001-powerpc-boot-wrapper-Add-z-notext-flag-for-ppc64le.patch': 'b296717ef0cd63978142b4d47b3bc49faa04daf77e115e702ba611cc254a6e782cdab7d5639e724cd5f029cfb6a3dc33c3036f346d681d1fdfe0df723c7e0321',
            '0002-x86-Compress-vmlinux-with-zstd-19-instead-of-22.patch': '055e10e7b3e00bb3621389315d5206a8feb5022a1f0dc51c3aa107facfe83d191da05a502a0c62488be7299677a63c1da84f87c42cbf3125788c56db67e810ee',
            '0003-kexec-add-kexec_load_disabled-boot-option.patch': 'e1d2d5358f5b8189236108973395f00eafce8db6d5baaf55025b1360f08ab7cffc930d3818719f220b98e7e8541cc8a7f056611b6e290f224a102c1addbe34ac',
            '0004-objtool-respect-AWK-setting.patch': '28c83be4c97152001381cd5e3a8acb8552a42d9dac9aaec8f096e3762b7944035aa5a4363fb4602f9bb427c4f72cfd2f996a0a725f7f9e07ab250317985aff9e',
            '0005-powerpc-config-defang-gcc-check-for-stack-protector-.patch': '5ce79416f8f111c36393718656e3eeb4cb0adccd117dcf661876d1564010e5c58b6351cc4116d187d0a9a838bcca0fecc236d81ac71afb940b89ccb14f21e179',
            '0001-Revert-LoongArch-Remove-some-extern-variables-in-sou.patch': '4ba26448c82f87c87baf7d1f89486caf5f4b6ea2345815bb392669d034c77505482e76431a3813ca01a503ac02c2ca65ee874f0afd5cddcfb3b60bbfdd4b07c7',
            'lts.aarch64.config': '26f8c4248ba3ac7fa6d0b369d36e495bf7e6dc90ad588ace764dbc4b1d4e48092696a000d0e6f523ee6409404e48e350db1ff640fb4e2b28e05052fb7f4d7694',
            'lts.armv7.config': 'a08046404bed1e2a5a94fdebc0cb9597f2b95450673b26d481a8687c7865feb6f8080b250f1232a0ae2349fdd394b102f326b81c9b790df7fb85088f5b2eb1c9',
            'lts.loongarch64.config': '65e3c10b5b6962ccb4c5628cf49fe21ac9033e4f07b958eda6fbd7fef9668160971af4fc990b51af070f66733fd4441d0faf4d2b596b2847e4b13535c92b7fe2',
            'lts.ppc64le.config': '9e40476a9e484b9dcded7f130c0cf8c99062daca83385f0c30827a064123ad8a39a3a315aecdc03c39dc844f798deacdfb0807522dd38822232c81fe8a311585',
            'lts.riscv64.config': '8a851d05ae99624ad7ec2d37a92565ac1d44e48270a3b30aa4f0bae910aed8ab7566bb70860c5b193c0f0a31fa981c02a4a318d824858f242761b5a31380b9d4',
            'lts.s390x.config': '1beffd66489287aa0a662b845d2f40e3b8ff2587ae31c62669acd26f795d876bd2f0f0cf118396978e9ca5c553423cb4b32a4921868a230f63b226a4bd4f0dc6',
            'lts.x86.config': '69e64967a9fdbe71a70beed94dd0c7c3a833f1ed8402e780e01eea7c72928ef9ff9cf84a5692794e40eaac853bbf186dea2a2cc6b7b17f130b5748c5887ed936',
            'lts.x86_64.config': '8645819be3ca70994fb055ce9543b7e2eb40d6a5bd2f14553d37c3cc9c2ea3c08a67a8a6d66987ec4f436f11cd00193fe5e1f374f015d152213c88ceed5ec002',
            'virt.aarch64.config': '0601435e6275ac2ce4c796a5626899082adb54fa14b15d77819c1cc698e7a8f71667a9d149334048bbe4bad93a5a1ec8cf9b8e06763086e0d7ead14c1623adab',
            'virt.armv7.config': 'dc9bf1b2687f436e9f75ee53c4e864cb9a5e4b62595e77bc00be341d2b224cc73b8e1718209d7cfb4376b3fb0c8f43d368e8f660d032aef45b8dbc0e6fc67956',
            'virt.ppc64le.config': '49fccd2169e185ab7f3dc787b8bdb0b22c7b9ad8264dacc54f1a1eb91600a5bd420c13b6e83d5ba914004597eb9e63860fdb11eeb52482219436c58844b6c3cd',
            'virt.x86.config': '8d94d29526e376b20a0e28e876a9fd561adc0023e7f3d5f774db61ce413f4a779dd1d2894f714b9b25feb41f3b35c00767dde1eea3e12a44421b1394ef5a6570',
            'virt.x86_64.config': '8749d34f17db7bd4c578e4fa6946e98dc070576b378d45e34c6a9cf2b972ad03b228402e0755132ee689ec6319d2f0c19055672d8d1703e571ae829b1e518224'}},
 {'origin': 'linux-tools',
  'commit': 'c4df575a9e2cc2474f7ef10151ca4dd9e5fab74a',
  'repo': 'community',
  'packages': [{'name': 'linux-tools-usbip', 'version': '6.15.0-r0', 'license': 'GPL-2.0-only', 'base': False}],
  'apkbuildSha256': '5add55cbb429a8c73a380757852e9b913c9c6666ff923deb4a1f02faca3634a6',
  'files': {'linux-6.15.tar.xz': 'd03788ffa8d8ae1b84ef1286bb44a08fc01432e509dfec6cccae5c5a5a47201d378aec2bcd21e6f0bbd1e625b26f47780c06ee9c1cef3775358f98b160923f30',
            'cpupower-libs.patch': 'a46e3a84b00a39a356618831d0ddfb7f0d10f0a3799d1307ba2cc832e73c01f8d637a4e801a6dd25025f6f13155c6ad8b836422ff72d365e51063ac0bf907f52',
            'dt_lnk.patch': '0657b461659e79c1926379aab30e7b7ab2d59eaf3e686f1dce468b79eb1320c188c14a0507361660abd2553bccffa9e149fa5b6e4d770d10c99bf5dafd2e05ee',
            'perf-doc.patch': '2092b96d64e6c379ae52700b3c1bf459581afea69622a296821381e74ecafa4a08cea24eab002ad2152878a4508918de7807ac98ba72faf2d4dea18cc275933f',
            'perf-basename.patch': '518a843bc4f11275e746a3fbcdbf126111f4f86f7f757033fa1b0e19ec1f8965bbfbcd9f170f9d33ab562b3e09743c080adc7fb8657f263ddcf120cf76b4da44',
            'bpftool-basename.patch': 'ea04b71c828df423e55ce22058015fe3cf13f58a1adbddefa61a8df26b343d22596d16737c592ead87a82df68672d44cbe4d4bfa4561185347db5c80ee9db340',
            'cpupower.initd': '97ecfeaaaa8efca7af585316b843bf7be885e88095bbdfbf52bc9a96b14dc862435482781e205ea022ab208978aaa4aabb317354fab890ed514a5088ae5045c8',
            'cpupower.confd': '63cd12a4d8782198c71662e9205e346e064b65bae5e3e175b5bc43bdf7ec6ddf675b60349750afe3c37c8a925f9ae9a93530671821342581bd282a69e0773b8c',
            'usbip.initd': 'fcbd64d844c9bc187d08cef5995e91a46c0df78deb24e96ac9210c0e2c730eca0301970d9b8ffbf003df274682d05072431a26b59d8c491f396618268a12ec92',
            'usbip.confd': 'eb8de617e27c4d5fdfee9c442e8f74b0afb4d0fe7b59eca3a19629eb70fea7e09b3c125bc968aa8810d845ce661c829bd0f3fdb2288664f2cccf423bc0ae6ae8'}},
 {'origin': 'readline',
  'commit': 'be0833caee3020f089f02c5d7223e971a9a11ffb',
  'repo': 'main',
  'packages': [{'name': 'readline', 'version': '8.2.13-r1', 'license': 'GPL-3.0-or-later', 'base': False}],
  'apkbuildSha256': '6572170e345db498debfe4acc97553e092fc7f23cfbb0035a24e937481376fe5',
  'files': {'readline-8.2.tar.gz': '0a451d459146bfdeecc9cdd94bda6a6416d3e93abd80885a40b334312f16eb890f8618a27ca26868cebbddf1224983e631b1cbc002c1a4d1cd0d65fba9fea49a',
            'fix-ncurses-underlinking.patch': 'b8e2f9a5c9294b49d35c2e1bbde523f01390dd3c8729f3a78e79799a236515ec723af795ba91b0a662a30e8c31df0f63bc80771e82cd31b4673f41428a5049ae',
            'fix-rl_do_undo-crash.patch': 'ddb9308005996106c4bc369ed5726c2479aeb73287ef30a1dc4d6c54fad78f8b1ea46d89b75bd7b00c54acd0ee0d9d17debf922e2260caea2df212e2f9d5109e',
            'inputrc': '5dbe872e94166aaed7ca2edec5a34ef9b13b254381e252cc6d851877b461579903cbb5b5dc588eabececcf1ebe636f6cb4da406cd01b64757f8c7e7f62e9a276',
            'readline82-001.patch': 'ea3800a62e992b3e594d78e77b6c4b662fb8c501b81010277e478656bb3b56db7747ba40c6c3d68c3ec5ab8e9e41be104788ec52268135d7e642ff40dea421a8',
            'readline82-002.patch': '2d4058c5c886498486d8514c238c0a05c0a97409011a7da40745e842977f0a29754e8b678f7e6e7a47c00df7e2e4c522d34d473118a24524af3824eed912e74a',
            'readline82-003.patch': '796df5a5bc4fc4077158621ac5359a7ccf81261e55880e6e70577ba24b50bd4fce4f2c70521d69a776f6ae8890d14d4f5c95cb9ba7c10f1065978c70626faeab',
            'readline82-004.patch': 'dc5e4cb6b7a058f433c9c6a587772a4c63e0aeef9c59f45f3f859a007cdbaa46dfd560c43cfe51b9c5c0a3a4d662c100540afc6026b2f8544984867ea3b52661',
            'readline82-005.patch': 'f20bcea5ce592edd0e41e0d725611ce536ceffc9ab176511ba6786ae802160b277c412ccbb2ce175da656bdfe5f0fd3f3ace11752cf1a152abff64c90f67a53a',
            'readline82-006.patch': '0a2737b2e566fafd60da8c0c7abcbaef24139165fb62a422d257b4cf38835b1e9b7a639c9e64228bbaac432d6a9ce62363b134284e55f3473df788f6433fbe63',
            'readline82-007.patch': 'e96dafe28f20b1092af08422b9be99965e3a973a0c19f7c57669a6a7fbf74ad4cc3490fa2ad50a1845644a65b0a0e001cfc7ad29528b3b36827a1b9a2c9a96e7',
            'readline82-008.patch': 'a92a00a6b6fefc576ef26cf7522525ac99c1a05de510ca8bd5a72a6db40fb4dc5c277931efa1f4a91e7cf72c9da5287bb14bfa8f0668d36bec40e3ccd7cbe418',
            'readline82-009.patch': '57c084c6186befac58a3432c483c9f879cd199d6947ad7dd29f0d60e67ae4ddf5ee8d861b0231b41582868dd6556ae2dda95a849caf11cbd3692fcd2f8663452',
            'readline82-010.patch': 'b611b42e31601ea5fcd7de59e6c1cdd3fece7cc8287e0e495ddfc4d5e8f765b3c6567b728fe0a317d6cb00240b772fa4c26f374c8bf361355a9d262bc5133874',
            'readline82-011.patch': '13694129f388364bea8009e1d2156c2a27e4b63bf780be2bc18129134e9f0454cad82106b6aeed03fab5ffe1f1fa630b80b77d1bc8b824a3d2280a71bf5d2ea6',
            'readline82-012.patch': '8921328ed899702a4e0f5c924efee7955d70909cc6ccd2722c87127891e0639376ef5a642ca4b27f92e0ee06d4fad7297fe5f2d2db425e1b6a9b1eaf5c1e4b63',
            'readline82-013.patch': '9a768946c91e4af31e872fb856b017662cdd5a12ed432d84ba13013fbac327673b655a034dbcfab52da2ff9e45eb1fdb562c3e26c224a5f4e86bff23df25175e'}},
 {'origin': 'socat',
  'commit': '81db0dd118ad2b7e90607790f29d28642bd6c7bf',
  'repo': 'main',
  'packages': [{'name': 'socat',
                'version': '1.8.1.3-r0',
                'license': 'GPL-2.0-only WITH OpenSSL-Exception',
                'base': False}],
  'apkbuildSha256': '6b642316a5633fc6fd9c9bf19777582ec81ce0cfa6bdd3ba5e7611fa3dbd379c',
  'files': {'socat-1.8.1.3.tar.gz': 'cd5f88a347fc7ea9431e235d7151d2007cd37143132eea625f7520b68e703e4c95f99fe6959869b3f0300633f039f666850cf3b0e26d43affd540967a119a3f3',
            'use-linux-headers.patch': '2032b6528cb27b69d8fb6a6f64af32fcc1f6e4934bb0d7c8931b38ab7ad5e27f6f4344a6cf49751fa3178cd725f954e195373362f7d5929e587d7f0309346059',
            'netdb-internal.patch': '22a6e0c2317a9317997c98114daac258ebbcc3d8e58e49a6ebf24781b98967afed47c63807282582fa0909076fe349281f05e4462faacb90e7aabc853903d6e6'}},
 {'origin': 'usbutils',
  'commit': 'ea15b2a43ef181be9807ee0099be6d6bf8d1df66',
  'repo': 'main',
  'packages': [{'name': 'usbutils', 'version': '018-r0', 'license': 'GPL-2.0-or-later', 'base': False}],
  'apkbuildSha256': '9ff4131c1213daaf03e416183ca94ba9b407c7685bcf4502536ea5b705fe536c',
  'files': {'usbutils-018.tar.gz': 'fa92aaeca16400b99aacee0df0f5cb68f8ade8331b6f14e896780f7f2eefe20a6ea69e1c51ad87d286c5e28bf0f68e9802e0e2035787c6f43a7fc0523f73f9cf'}}]

# APK install/trigger hooks are pinned to official aports git blobs.
APORTS_EXTRAS = [{'origin': 'alpine-baselayout',
  'name': 'alpine-baselayout.post-install',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/540d8477019887cc6534152e92c3b12676e35d7b/main/alpine-baselayout/alpine-baselayout.post-install',
  'sha256': 'dd30b63bfbe4f7d8085f57840e15b84015b3518d6f282a3e0924f73a61000016',
  'gitBlob': '88966d35c3327efc03e5d383fa577782ea256c5d'},
 {'origin': 'alpine-baselayout',
  'name': 'alpine-baselayout.post-upgrade',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/540d8477019887cc6534152e92c3b12676e35d7b/main/alpine-baselayout/alpine-baselayout.post-upgrade',
  'sha256': '53f570f015ac2086597ab62accc6ad08600e2994577ac622263f17e9de39d2e2',
  'gitBlob': '0e73fc07f3bfd966d5ed2e4be2a4838422dab0ed'},
 {'origin': 'alpine-baselayout',
  'name': 'alpine-baselayout.pre-install',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/540d8477019887cc6534152e92c3b12676e35d7b/main/alpine-baselayout/alpine-baselayout.pre-install',
  'sha256': '28ecff91a36f438ca26bc8cc08d857863378aa94eab399a1f413e52501b86f85',
  'gitBlob': 'fc0d0b9f5dab5ce07189ead013e0fb8a6fbc0bc8'},
 {'origin': 'alpine-baselayout',
  'name': 'alpine-baselayout.pre-upgrade',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/540d8477019887cc6534152e92c3b12676e35d7b/main/alpine-baselayout/alpine-baselayout.pre-upgrade',
  'sha256': '3f35fa5402d397389f366e8934ba0f800a017a149f4281dc8c22762419e47e10',
  'gitBlob': 'b89ad05a74debf8a6f6001b05526770feb40ee88'},
 {'origin': 'busybox',
  'name': 'busybox-extras.post-install',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/2e97d754a30d558f15524b8e422303c8a96832df/main/busybox/busybox-extras.post-install',
  'sha256': 'd9fc2353305920237a46b75c8b184166e6a2f9b6aaba5c96ed80d5234534141c',
  'gitBlob': 'dc663a554fd7a8fc1a3c1e15477fae249a399dee'},
 {'origin': 'busybox',
  'name': 'busybox-extras.pre-deinstall',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/2e97d754a30d558f15524b8e422303c8a96832df/main/busybox/busybox-extras.pre-deinstall',
  'sha256': '921289ece87e6a645d15dcd7fea4cd96e3c1a3ad393f7a98f6cad03bb7266aea',
  'gitBlob': '2634c6a31cb022233b8e111d3884e9a768c1f9b9'},
 {'origin': 'busybox',
  'name': 'busybox.post-install',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/2e97d754a30d558f15524b8e422303c8a96832df/main/busybox/busybox.post-install',
  'sha256': '13c120a1c714ecafa7fe1fc748406a89dfffc549118ebce324b53d4bbb4ba75f',
  'gitBlob': 'f575396bb1a66c0a4f07c838ee908fc08201cc2d'},
 {'origin': 'busybox',
  'name': 'busybox.post-upgrade',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/2e97d754a30d558f15524b8e422303c8a96832df/main/busybox/busybox.post-upgrade',
  'sha256': '7a889f45d8e221698c18e315e38ea243cab0cc12935aa10f5e6adce2e4f3f991',
  'gitBlob': '98822a226f756554b56115a5d6f14552808278dd'},
 {'origin': 'busybox',
  'name': 'busybox.trigger',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/2e97d754a30d558f15524b8e422303c8a96832df/main/busybox/busybox.trigger',
  'sha256': '56be2f4b9f99be9fc281314cda5c5e3b45a8123e37ba4938f1405584ce1af443',
  'gitBlob': '65a9b6b5bb0dd764829451f7175f3fdead8746fb'},
 {'origin': 'ca-certificates',
  'name': 'ca-certificates.post-deinstall',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/ce7bff87478cd936f5b7031ce822b02d4fdd313a/main/ca-certificates/ca-certificates.post-deinstall',
  'sha256': '9523860edc396093bd41270c6e33a569a30657e2ffd729829514ee6b6d54d652',
  'gitBlob': '10d219f03f526641af8a366db975b010b3c260e1'},
 {'origin': 'ca-certificates',
  'name': 'ca-certificates.trigger',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/ce7bff87478cd936f5b7031ce822b02d4fdd313a/main/ca-certificates/ca-certificates.trigger',
  'sha256': '1bf77c8eae3220b1bd7dd86561d0899315f51d72257c1aeb4066072ed4304fe5',
  'gitBlob': '0ed2ac4980c97739fb6addfbf41a5700c6d286a1'},
 {'origin': 'eudev',
  'name': 'eudev-hwids.trigger',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/8bac74716d06c84627ab02d101fd9d2bafd0a34c/main/eudev/eudev-hwids.trigger',
  'sha256': '1003167002fc60e6d5685b1c8f656df180f4d3c9977a5c2bf4f0d54dd04cd02b',
  'gitBlob': 'b1516251b0570922ae88d82b497cad98bde38196'},
 {'origin': 'eudev',
  'name': 'eudev.trigger',
  'url': 'https://gitlab.alpinelinux.org/alpine/aports/-/raw/8bac74716d06c84627ab02d101fd9d2bafd0a34c/main/eudev/eudev.trigger',
  'sha256': '104ae036a067c31b743544e7c19815579673387cf1fc4724db6e75774358333b',
  'gitBlob': 'f200c38c56200877a4378c7b7023945875cc4342'}]

COMMIT = "b78e31e93e84b4cb7c4b4c15b8aace10749106d2"
BACKEND_ARCHIVES = {
    "go1.27.3.darwin-arm64.tar.gz": "dd3908cf20959d798c1e06ebe5b72213f0505ce01373b40ce3110975a6cd99c2",
    "VIIPER-" + COMMIT + ".zip": "8305750e881e7e955dcf26dc5fedc2b43d55902c9f1fa5b4eca31fe51901b2a5",
}
GO_MODULES = [
    ("golang.org/x/crypto", "v0.52.0", "h1:RMs7fP2rXdep0CftQlK8Uf+kibLm7qkCcradZWYz988=",
     "a4dca44f467a3d8745aef41147c7bc6a90cdcfda9c42ae0585888bf046c7941e"),
    ("golang.org/x/exp", "v0.0.0-20260410095643-746e56fc9e2f", "h1:W3F4c+6OLc6H2lb//N1q4WpJkhzJCK5J6kUi1NTVXfM=",
     "de285d08489fcd4d420a8561501e75b5124db0a48121dfc26e811959f518db4e"),
]

# External names are APKBUILD aliases, including the readline patch suffixes.
# The official Alpine distfile mirror is preferred; upstream is a fallback.
EXTERNAL = {
    "protocols-6.4": "https://salsa.debian.org/md/netbase/-/raw/v6.4/etc/protocols",
    "services-6.4": "https://salsa.debian.org/md/netbase/-/raw/v6.4/etc/services",
    "apk-tools-v2.14.12.tar.gz": "https://gitlab.alpinelinux.org/alpine/apk-tools/-/archive/v2.14.12/apk-tools-v2.14.12.tar.gz",
    "busybox-1.37.0.tar.bz2": "https://busybox.net/downloads/busybox-1.37.0.tar.bz2",
    "ca-certificates-20261020.tar.bz2": "https://gitlab.alpinelinux.org/alpine/ca-certificates/-/archive/20261020/ca-certificates-20261020.tar.bz2",
    "openssl-3.5.9.tar.gz": "https://github.com/openssl/openssl/releases/download/openssl-3.5.9/openssl-3.5.9.tar.gz",
    "musl-1.2.5.tar.gz": "https://musl.libc.org/releases/musl-1.2.5.tar.gz",
    "pax-utils-1.3.8.tar.xz": "https://dev.gentoo.org/~sam/distfiles/app-misc/pax-utils/pax-utils-1.3.8.tar.xz",
    "zlib-1.3.2.tar.gz": "https://zlib.net/fossils/zlib-1.3.2.tar.gz",
    "eudev-3.2.14.tar.gz": "https://github.com/eudev-project/eudev/releases/download/v3.2.14/eudev-3.2.14.tar.gz",
    "hwdata-0.395.tar.gz": "https://github.com/vcrhonek/hwdata/archive/refs/tags/v0.395.tar.gz",
    "ncurses-6.5-20250503.tgz": "https://invisible-mirror.net/archives/ncurses/current/ncurses-6.5-20250503.tgz",
    "libusb-1.0.28.tar.bz2": "https://github.com/libusb/libusb/releases/download/v1.0.28/libusb-1.0.28.tar.bz2",
    "linux-6.12.tar.xz": "https://cdn.kernel.org/pub/linux/kernel/v6.x/linux-6.12.tar.xz",
    "patch-6.12.114.patch.xz": "https://cdn.kernel.org/pub/linux/kernel/v6.x/patch-6.12.114.xz",
    "linux-6.15.tar.xz": "https://kernel.org/pub/linux/kernel/v6.x/linux-6.15.tar.xz",
    "readline-8.2.tar.gz": "https://ftp.gnu.org/gnu/readline/readline-8.2.tar.gz",
    "socat-1.8.1.3.tar.gz": "http://www.dest-unreach.org/socat/download/socat-1.8.1.3.tar.gz",
    "usbutils-018.tar.gz": "https://github.com/gregkh/usbutils/archive/refs/tags/v018.tar.gz",
}
for number in range(1, 14):
    EXTERNAL[f"readline82-{number:03}.patch"] = f"https://ftp.gnu.org/gnu/readline/readline-8.2-patches/readline82-{number:03}"


def digest_file(path, algorithm="sha256"):
    digest = hashlib.new(algorithm)
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def verified_file(path, expected, algorithm="sha256"):
    if path.is_symlink() or not path.is_file() or digest_file(path, algorithm) != expected:
        raise ValueError("Missing, unsafe or checksum-mismatched file: " + path.name)


def fetch(path, urls, expected, algorithm):
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() or path.is_symlink():
        verified_file(path, expected, algorithm)
        return {"path": str(path), "url": urls[0], algorithm: expected, "size": path.stat().st_size}
    partial = path.with_name("." + path.name + "." + uuid.uuid4().hex + ".part")
    errors = []
    try:
        for url in urls:
            for attempt in range(2):
                try:
                    digest = hashlib.new(algorithm)
                    size = 0
                    with urllib.request.urlopen(url, timeout=30) as response, partial.open("wb") as stream:
                        while True:
                            chunk = response.read(1024 * 1024)
                            if not chunk:
                                break
                            size += len(chunk)
                            if size > 700 * 1024 * 1024:
                                raise ValueError("Source archive exceeds size bound")
                            stream.write(chunk)
                            digest.update(chunk)
                    if digest.hexdigest() != expected:
                        raise ValueError("Source checksum differs from pinned APKBUILD")
                    partial.replace(path)
                    return {"path": str(path), "url": url, algorithm: expected, "size": size}
                except (OSError, ValueError) as error:
                    errors.append(type(error).__name__ + ": " + str(error))
        raise RuntimeError("Failed to collect " + path.name + ": " + "; ".join(errors))
    finally:
        partial.unlink(missing_ok=True)


def alpine_tasks(output):
    for origin in ALPINE:
        directory = output / "alpine" / origin["origin"]
        base = "https://gitlab.alpinelinux.org/alpine/aports/-/raw/" + origin["commit"] + "/" + origin["repo"] + "/" + origin["origin"] + "/"
        yield directory / "APKBUILD", [base + "APKBUILD"], origin["apkbuildSha256"], "sha256"
        for filename, expected in origin["files"].items():
            if filename in EXTERNAL:
                urls = ["https://distfiles.alpinelinux.org/distfiles/v3.22/" + filename, EXTERNAL[filename]]
            else:
                urls = [base + filename]
            yield directory / filename, urls, expected, "sha512"
    for extra in APORTS_EXTRAS:
        yield output / "alpine" / extra["origin"] / extra["name"], [extra["url"]], extra["sha256"], "sha256"


def copy_verified(source, destination, expected):
    verified_file(source, expected)
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() or destination.is_symlink():
        verified_file(destination, expected)
    else:
        shutil.copyfile(source, destination)
    return {"path": str(destination), "sha256": expected, "size": destination.stat().st_size}


def copy_text(source, destination):
    text = source.read_text()
    if str(Path.home()) + "/" in text:
        raise ValueError("Refusing private absolute path in source text: " + source.name)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(text)
    return {"path": str(destination), "sha256": digest_file(destination), "size": destination.stat().st_size}


def go_zip_hash(path):
    lines = []
    with zipfile.ZipFile(path) as archive:
        for name in sorted(archive.namelist()):
            lines.append(hashlib.sha256(archive.read(name)).hexdigest() + "  " + name + "\n")
    return "h1:" + base64.b64encode(hashlib.sha256("".join(lines).encode()).digest()).decode()


def collect_backend(cache, output, repository):
    files = []
    build = json.loads((cache / "BUILD.json").read_text())
    for name, expected in BACKEND_ARCHIVES.items():
        files.append(copy_verified(cache / "downloads" / name, output / "backend/downloads" / name, expected))
    go_sum = (cache / "module/go.sum").read_text()
    for module, version, expected_h1, expected_sha256 in GO_MODULES:
        source = cache / "gomodcache/cache/download" / module / "@v" / (version + ".zip")
        if module + " " + version + " " + expected_h1 not in go_sum or go_zip_hash(source) != expected_h1:
            raise ValueError("Go module content does not match the pinned go.sum")
        destination = output / "backend/modules" / (module.replace("/", "_") + "@" + version + ".zip")
        item = copy_verified(source, destination, expected_sha256)
        item.update(module=module, version=version, goSum=expected_h1,
                    url="https://proxy.golang.org/" + module + "/@v/" + version + ".zip")
        files.append(item)
    for name in ["main.go", "main_test.go", "go.mod", "go.sum"]:
        if name in build["moduleSourceSha256"]:
            verified_file(cache / "module" / name, build["moduleSourceSha256"][name])
        if name == "go.sum":
            verified_file(cache / "module" / name, build["goSumSha256"])
        files.append(copy_text(cache / "module" / name, output / "backend/module" / name))
    for record in json.loads((cache / "MATERIALS.json").read_text()):
        files.append(copy_verified(cache / record["license"], output / "notices/backend" / Path(record["license"]).name,
                                   record["licenseSha256"]))
    for relative in ["research/macos-native/guest-backend/build.py",
                     "research/macos-native/guest-backend/main.go",
                     "research/macos-native/guest-backend/main_test.go",
                     "research/macos-native/guest-backend/autoattach_darwin.go.overlay",
                     "research/macos-native/guest/prepare_guest.py",
                     "packaging/macos/prepare_runtime.py", "packaging/macos/collect_sources.py"]:
        files.append(copy_text(repository / relative, output / "rebuild" / relative))
    # Keep correction explicit; do not rewrite the historical local build.
    receipt = {"sourceCommit": COMMIT, "goVersion": "go1.27.3",
               "historicalGoVersionCorrection": {"recorded": build["goVersion"],
                   "correct": "go version go1.27.3 darwin/arm64",
                   "reason": "module loop shadowed the toolchain version variable; VERSION and pinned toolchain archive corroborate correction"},
               "runtimeModules": [{"module": m, "version": v, "goSum": h} for m, v, h, _ in GO_MODULES],
               "binaries": build["binaries"], "cgoEnabled": False,
               "moduleSourceSha256": build["moduleSourceSha256"], "upstreamArchiveSha256": BACKEND_ARCHIVES["VIIPER-" + COMMIT + ".zip"],
               "upstreamURL": "https://codeload.github.com/hbashton/VIIPER/zip/" + COMMIT,
               "toolchainURL": "https://go.dev/dl/go1.27.3.darwin-arm64.tar.gz"}
    (output / "backend/MATERIALS.json").write_text(json.dumps(receipt, indent=2) + "\n")
    return files


def collect_notices(output):
    names = {"COPYING", "COPYING.LESSER", "COPYING.LIB", "LICENSE", "LICENSE.TXT", "LICENCE", "COPYRIGHT", "NOTICE", "NOTICE.TXT"}
    count = 0
    for origin in ALPINE:
        directory = output / "alpine" / origin["origin"]
        notice_directory = output / "notices/alpine" / origin["origin"]
        notice_directory.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(directory / "APKBUILD", notice_directory / "APKBUILD.txt")
        for filename in origin["files"]:
            if not filename.endswith((".tar.gz", ".tar.bz2", ".tar.xz", ".tgz")):
                continue
            with tarfile.open(directory / filename, "r:*") as archive:
                for member in archive:
                    parts = PurePosixPath(member.name).parts
                    if not (member.isfile() or member.issym()) or member.size > 2 * 1024 * 1024 or ".." in parts or not parts:
                        continue
                    basename = PurePosixPath(member.name).name.upper()
                    top_notice = len(parts) <= 3 and (basename in names or basename.startswith("COPYING."))
                    license_directory = len(parts) >= 3 and parts[1] == "LICENSES"
                    ca_notice = origin["origin"] == "ca-certificates" and basename == "CERTDATA.TXT"
                    if not (top_notice or license_directory or ca_notice):
                        continue
                    destination = output / "notices/alpine" / origin["origin"] / Path(*parts[1:])
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    data = archive.extractfile(member).read(2 * 1024 * 1024 + 1)
                    if len(data) > 2 * 1024 * 1024:
                        raise ValueError("Unexpected oversized license file")
                    if ca_notice:
                        # Preserve the upstream source-form notice verbatim;
                        # the complete certdata is in the source archive.
                        data = b"\n".join(data.splitlines()[:5]) + b"\n"
                        destination = destination.with_name("certdata-LICENSE-NOTICE.txt")
                    destination.write_bytes(data)
                    count += 1
    print("Preserved", count, "upstream licence/notice files; complete source archives also retain all notices.", flush=True)


def write_readme(output):
    text = """# Mac runtime corresponding sources

This directory accompanies the bundled aarch64 Linux runtime and the pinned
GuestDualSenseBackend. It contains 19 Alpine source origins, their exact
APKBUILD recipes, every checksummed source/config/patch input, APK install and
trigger hooks, the complete VIIPER source archive, the Go 1.27.3 toolchain
(including its source), and both actually linked nonstandard Go modules.
MATERIALS.json records immutable URLs and hashes; notices/ preserves upstream
licences and package metadata. No private guest identity, keys, logs, signing
credentials, Sony binaries or VirtualHere binaries are included.

## Rebuilding Alpine packages

Use an aarch64 Alpine 3.22 build environment with alpine-sdk/abuild. Each
alpine/<origin>/APKBUILD declares its build dependencies, subpackages, patch
order, configuration, and build/install commands. Keep every file next to its
APKBUILD, run abuild checksum verification, then abuild -r as an unprivileged
Alpine build user. The collected archives are already the names expected by
the recipes; an SRCDEST cache can reuse them. These recipes were collected and
verified as data; this collection did not execute them or claim a fresh
source compilation of all 19 packages.

The kernel Image and selected modules come from linux-lts 6.12.114-r0: Linux
6.12 plus patch-6.12.114 and the Alpine patch set, built with lts.aarch64.config.
The usbip executable comes from the separate Linux 6.15 tools source tree.
Do not substitute either source tree for the other.

## Reproducing the tested kernel/initrd bytes

Use the pinned original rootfs/APK/index inputs from runtime/guest-manifest.json
and the allowlist in rebuild/packaging/macos/prepare_runtime.py. Run the latter
with --cache <pinned-input-directory> --output <new-directory>. It verifies
every input, performs signature/data validation, reconstructs the Image and
initrd, and compares their exact tested hashes. It never downloads or boots.
dummy and af_packet are deliberately not early initrd modules; the runtime
sets its own per-install identity before loading the separately supplied
modules. Rebuilding modified source requires intentionally updating the
expected output hashes; original hashes do not attest modified binaries.

## Rebuilding the backend

backend/module contains the exact formatted main.go/main_test.go, go.mod and
go.sum used for the shipped backend. Unpack the VIIPER source ZIP beneath
backend/upstream so the relative replacement path in go.mod resolves. The
pinned toolchain archive unpacks as go/. Runtime modules are preserved as
unmodified Go module ZIPs with hashes checked against go.sum. The matching
Go proxy URLs are in MATERIALS.json. The repository's source build script is
in rebuild/research/macos-native/guest-backend/build.py; it documents the
CGO_ENABLED=0, arm64 Linux/Darwin flags, pure tests and Darwin overlay. It never
runs the backend binaries. Its goVersion record now retains the toolchain
version instead of the last module version; backend/MATERIALS.json explains
the historical metadata correction without changing the historical build.

This runtime source collection is distributed alongside the application's
own source snapshot and build/installation scripts. The application source
and this runtime collection must refer to the same published binary version.
"""
    (output / "README.md").write_text(text)
    lines = ["# Third-party runtime notices", "", "Full licences and original package metadata are in the subdirectories.",
             "Complete corresponding sources accompany this release; their exact versions and hashes are in MATERIALS.json.", "",
             "| Source origin | Included binary packages | Package-reported licence |", "| --- | --- | --- |"]
    for origin in ALPINE:
        packages = ", ".join(p["name"] + " " + p["version"] for p in origin["packages"])
        licenses = "; ".join(dict.fromkeys(p["license"] for p in origin["packages"]))
        lines.append("| " + origin["origin"] + " | " + packages + " | " + licenses + " |")
    lines += ["", "VIIPER commit " + COMMIT + " retains its GPLv3 licence; Go 1.27.3, golang.org/x/crypto v0.52.0 and golang.org/x/exp v0.0.0-20260410095643-746e56fc9e2f retain their upstream BSD notices in backend/.",
              "", "The package licence column is copied from Alpine metadata. Per-file licence terms and copyright notices remain in the original sources and extracted notice files; no authorship is reassigned.", ""]
    (output / "notices/README.md").write_text("\n".join(lines))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--backend-cache", type=Path, required=True)
    parser.add_argument("--archive", type=Path, help="Optional new deterministic .tar.gz output")
    args = parser.parse_args()
    output = args.output.absolute()
    if output.is_symlink():
        raise ValueError("Output may not be a symlink")
    marker = output / ".pskeyboard-runtime-sources"
    if output.exists() and (not marker.is_file() or marker.read_text() != "pinned-runtime-sources-v1\n"):
        raise ValueError("Refusing an unrelated existing output directory")
    output.mkdir(parents=True, exist_ok=True)
    marker.write_text("pinned-runtime-sources-v1\n")
    tasks = list(alpine_tasks(output))
    results = []
    with ThreadPoolExecutor(max_workers=4) as pool:
        pending = {pool.submit(fetch, *task): task[0] for task in tasks}
        for completed in as_completed(pending):
            item = completed.result()
            results.append(item)
            print(f"Verified {len(results)}/{len(tasks)}: {pending[completed].parent.name}/{pending[completed].name} ({item['size']} bytes)", flush=True)
    repository = Path(__file__).resolve().parents[2]
    results += collect_backend(args.backend_cache.resolve(strict=True), output, repository)
    results.append(copy_verified(repository / ".local/mac-usb-guest-1/guest-manifest.json",
                                 output / "runtime/guest-manifest.json",
                                 "c2021bd548484b77632cfdcf20e70fb13a2d00348b66c6c905541e9a59562a5f"))
    collect_notices(output)
    write_readme(output)
    for record in results:
        record["path"] = str(Path(record["path"]).relative_to(output))
    manifest = {"schemaVersion": 1, "scope": "exact Alpine guest and guest backend corresponding sources",
                "architecture": "aarch64", "alpineRootfs": "3.22.7", "origins": ALPINE,
                "aportsExtraHooks": APORTS_EXTRAS,
                "files": sorted(results, key=lambda item: item["path"]),
                "executedDownloadedCode": False, "copiedPrivateIdentityOrLogs": False}
    (output / "MATERIALS.json").write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n")
    checksums = []
    for path in sorted(output.rglob("*")):
        if path.is_symlink():
            raise ValueError("Unexpected symlink in source collection")
        if path.is_file() and path.name != "FILES.sha256":
            checksums.append(digest_file(path) + "  " + str(path.relative_to(output)))
    (output / "FILES.sha256").write_text("\n".join(checksums) + "\n")
    if args.archive:
        archive_path = args.archive.absolute()
        if archive_path.exists() or archive_path.is_symlink() or output in archive_path.parents:
            raise ValueError("Archive must be a new file outside the source directory")
        archive_path.parent.mkdir(parents=True, exist_ok=True)
        partial = archive_path.with_name("." + archive_path.name + "." + uuid.uuid4().hex + ".part")
        try:
            with partial.open("wb") as raw, gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0) as compressed:
                with tarfile.open(fileobj=compressed, mode="w|") as archive:
                    for path in sorted(output.rglob("*")):
                        relative = Path("mac-runtime-sources") / path.relative_to(output)
                        info = archive.gettarinfo(str(path), arcname=str(relative))
                        info.uid = info.gid = 0
                        info.uname = info.gname = ""
                        info.mtime = 0
                        info.mode = 0o755 if path.is_dir() else 0o644
                        info.pax_headers = {}
                        if path.is_file():
                            with path.open("rb") as stream:
                                archive.addfile(info, stream)
                        else:
                            archive.addfile(info)
            partial.replace(archive_path)
        finally:
            partial.unlink(missing_ok=True)
        receipt = {"file": archive_path.name, "sha256": digest_file(archive_path),
                   "size": archive_path.stat().st_size, "sourceFiles": len(checksums),
                   "sourceOrigins": len(ALPINE), "privateIdentityOrLogsIncluded": False}
        archive_path.with_name(archive_path.name + ".json").write_text(json.dumps(receipt, indent=2) + "\n")
        print("SOURCE ARCHIVE:", json.dumps(receipt), flush=True)
    print("COMPLETE:", len(ALPINE), "Alpine origins, pinned backend sources, notices and rebuild scripts.", flush=True)


if __name__ == "__main__":
    main()

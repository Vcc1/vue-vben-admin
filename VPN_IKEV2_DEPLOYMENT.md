# IKEv2 VPN Deployment Notes

Date: 2026-05-27

## Environment

- Router OS: iStoreOS 24.10.5
- Router SSH: `root@192.168.50.5`
- Main router: `192.168.50.1`
- VPN domain: `home.zhw.name`
- VPN type: strongSwan IKEv2, `swanctl`
- VPN address pool: `10.8.0.0/24`
- VPN client observed address: `10.8.0.1`
- Tunnel mode: full tunnel, `local_ts = 0.0.0.0/0`

## Client Settings

iPhone IKEv2 settings:

- Server: `home.zhw.name`
- Remote ID: `home.zhw.name`
- Local ID: empty
- Username: `IPhone`
- Password: `sr19910823.`
- Authentication: username/password
- Proxy: off

Because the server now uses an official Certum wildcard certificate, iPhone does not need the previously generated self-signed CA certificate.

## Installed Packages

Installed strongSwan packages/modules included:

- `strongswan`
- `strongswan-charon`
- `strongswan-swanctl`
- `strongswan-pki`
- `strongswan-mod-aes`
- `strongswan-mod-sha1`
- `strongswan-mod-sha2`
- `strongswan-mod-random`
- `strongswan-mod-pubkey`
- `strongswan-mod-gmp`
- `strongswan-mod-gmpdh`
- `strongswan-mod-hmac`
- `strongswan-mod-socket-default`
- `strongswan-mod-kernel-netlink`
- `strongswan-mod-vici`
- `strongswan-mod-x509`
- `strongswan-mod-pem`
- `strongswan-mod-pkcs1`
- `strongswan-mod-pkcs8`
- `strongswan-mod-attr`
- `strongswan-mod-eap-identity`
- `strongswan-mod-eap-mschapv2`
- `strongswan-mod-md4`
- `strongswan-mod-md5`
- `strongswan-mod-mgf1`
- `strongswan-mod-drbg`
- `strongswan-mod-kdf`
- `strongswan-mod-openssl`
- `strongswan-mod-resolve`
- `strongswan-mod-revocation`
- `strongswan-mod-updown`

Service name on this iStoreOS build:

```sh
/etc/init.d/swanctl
```

## Certificate

The official wildcard certificate zip was checked from:

```text
C:\Users\admin\Downloads\wildcard_zhw.name_cert (2).zip
```

Validated certificate details:

- Subject: `CN=*.zhw.name`
- SAN: `DNS:*.zhw.name, DNS:zhw.name`
- Covers: `home.zhw.name`
- Issuer: `Certum DV TLS G2 R39 CA`
- Validity: 2026-04-16 to 2026-11-01
- Private key matched the certificate

Deployed on iStoreOS:

- Server certificate: `/etc/swanctl/x509/server-cert.pem`
- Server private key: `/etc/swanctl/private/server-key.pem`
- Certum chain: `/etc/swanctl/x509ca/certum-chain.pem`

Old self-signed CA files were removed from the active runtime directory. Backups were stored under `/root/vpn-backup/`.

## strongSwan Config

Main config:

```sh
/etc/swanctl/swanctl.conf
```

Important current values:

```text
connections {
  ikev2-vpn {
    version = 2
    local_addrs = 0.0.0.0
    pools = vpn-pool
    send_cert = always
    unique = replace

    local {
      auth = pubkey
      certs = server-cert.pem
      id = home.zhw.name
    }

    remote {
      auth = eap-mschapv2
      eap_id = %any
    }

    children {
      net {
        local_ts = 0.0.0.0/0
        start_action = none
      }
    }
  }
}

pools {
  vpn-pool {
    addrs = 10.8.0.0/24
    dns = 223.5.5.5, 119.29.29.29
  }
}

secrets {
  eap-v {
    id = IPhone
    secret = "sr19910823."
  }
}
```

Reload/check commands:

```sh
/etc/init.d/swanctl restart
swanctl --load-all --noprompt
swanctl --list-conns
swanctl --list-sas
swanctl --list-certs
```

Expected listener ports:

```sh
netstat -lunp | grep -E ':500|:4500'
```

Expected result: `charon` listening on UDP `500` and `4500`.

## Firewall And NAT

IPv4 forwarding was enabled:

```sh
net.ipv4.ip_forward=1
```

Persistent firewall UCI entries include:

- `VPN-Masquerade`: masquerades `10.8.0.0/24`
- `Allow-IKEv2-Input`: allows UDP `500 4500`
- include script: `/etc/firewall.vpn_ikev2.sh`

Compatibility firewall script created:

```sh
/etc/firewall.vpn_ikev2.sh
```

Script contents:

```sh
#!/bin/sh
iptables -t nat -C POSTROUTING -s 10.8.0.0/24 -j MASQUERADE 2>/dev/null || iptables -t nat -I POSTROUTING 1 -s 10.8.0.0/24 -j MASQUERADE
iptables -C FORWARD -s 10.8.0.0/24 -j ACCEPT 2>/dev/null || iptables -I FORWARD 1 -s 10.8.0.0/24 -j ACCEPT
iptables -C FORWARD -d 10.8.0.0/24 -m conntrack --ctstate RELATED,ESTABLISHED -j ACCEPT 2>/dev/null || iptables -I FORWARD 1 -d 10.8.0.0/24 -m conntrack --ctstate RELATED,ESTABLISHED -j ACCEPT
iptables -t mangle -C FORWARD -p tcp --tcp-flags SYN,RST SYN -s 10.8.0.0/24 -j TCPMSS --clamp-mss-to-pmtu 2>/dev/null || iptables -t mangle -I FORWARD 1 -p tcp --tcp-flags SYN,RST SYN -s 10.8.0.0/24 -j TCPMSS --clamp-mss-to-pmtu
```

This script was registered in OpenWrt firewall includes with:

```text
firewall.@include[1].path='/etc/firewall.vpn_ikev2.sh'
firewall.@include[1].fw4_compatible='1'
```

Useful checks:

```sh
iptables -t nat -S POSTROUTING | grep '10.8.0.0/24'
iptables -S FORWARD | grep '10.8.0.0/24'
iptables -t mangle -S FORWARD | grep '10.8.0.0/24'
nft list ruleset | grep -C 3 '10.8.0.0'
```

## DNS Fix

The VPN originally used `192.168.50.5` as DNS. When the iPhone connected but had no internet, the pool DNS was changed to public DNS:

```text
dns = 223.5.5.5, 119.29.29.29
```

`dnsmasq localservice` was also disabled so DNS is not restricted to only local directly connected subnets:

```sh
uci set dhcp.@dnsmasq[0].localservice='0'
uci commit dhcp
/etc/init.d/dnsmasq restart
```

## Nikki / NodeBaby Notes

Nikki/mihomo was running and listening on:

- `1053`
- `1080`
- `7890`
- `7891`
- `7892`
- `8080`
- `9090`

Observed process:

```text
/usr/bin/mihomo -d /etc/nikki/run
```

The final "connected but no internet" fix was the DNS change plus explicit `iptables` compatibility NAT/FORWARD/MSS rules for `10.8.0.0/24`.

If internet breaks again, first reconnect the iPhone VPN, then check:

```sh
swanctl --list-sas
iptables -t nat -S POSTROUTING | grep '10.8.0.0/24'
iptables -S FORWARD | grep '10.8.0.0/24'
grep -n 'dns =' /etc/swanctl/swanctl.conf
uci get dhcp.@dnsmasq[0].localservice
```

## Main Router Requirement

The upstream/main router must forward these ports to iStoreOS `192.168.50.5`:

- UDP `500`
- UDP `4500`

Without these forwards, external IKEv2 clients cannot connect from outside the LAN.

## Ansible のインストール

Ansible を実行するマシン(他のマシンの IP アドレスなどを指定して，セットアップさせるマシン)に
Ansible をインストールする

- MacOS の場合

```bash
brew install ansible
```

## ./hosts を編集

```ini:hosts
[ome_server]
192.168.50.231:22
# 172.104.78.212:22

[ome_server:vars]
ansible_user='myoshimi'
ansible_ssh_private_key_file='~/.ssh/id_rsa'
```

```ini:hosts
[<グループ名セットアップしたいマシンのグループ名>]
<IPアドレスなど>:<SSHポート番号>

[<グループ名>:vars]
ansible_user='<ログインユーザ名>'
ansible_ssh_private_key_file='<SSH秘密鍵のパス>'
```

## テスト

```bash
ansible -i hosts 192.168.50.231 -m ping

192.168.50.231 | SUCCESS => {
    "ansible_facts": {
        "discovered_interpreter_python": "/usr/bin/python3"
    },
    "changed": false,
    "ping": "pong"
}
```

## プレイブックの実行

### dry-run

```bash
ansible-playbook playbook.yml -i hosts -C
```

- docker-ce が見つからない，と言われる場合アリ

### 実行

```bash
ansible-playbook playbook.yml -i hosts
```

#!/bin/bash

if [ -z "$KEYS_MOUNT_PATH" ]; then
  KEYS_MOUNT_PATH=/mnt/efs/keys
fi
if [ -z "$CONF_MOUNT_PATH" ]; then
  CONF_MOUNT_PATH=/mnt/efs/conf
fi
KEYS_OME_PATH=/opt/ovenmediaengine/bin
CONF_OME_PATH=/opt/ovenmediaengine/bin/origin_conf

KEYS_FILES=( \
  chain.pem \
  privkey.pem \
  cert.pem \
)

for i in "${KEYS_FILES[@]}"
do
    rm -f ${KEYS_OME_PATH}/${i}
    ln -s ${KEYS_MOUNT_PATH}/${i} ${KEYS_OME_PATH}
done

rm -f ${CONF_OME_PATH}/Server.xml
ln -s ${CONF_MOUNT_PATH}/Server.xml ${CONF_OME_PATH}

/opt/ovenmediaengine/bin/OvenMediaEngine -c origin_conf


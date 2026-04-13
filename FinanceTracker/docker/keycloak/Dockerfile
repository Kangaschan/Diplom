FROM quay.io/keycloak/keycloak:26.2

COPY docker/keycloak/realm/financetracker-realm.json /opt/keycloak/data/import/financetracker-realm.json

ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start-dev", "--import-realm", "--http-port=8080"]

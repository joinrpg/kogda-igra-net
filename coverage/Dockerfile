# Образ PHP-версии с Xdebug — только для замера покрытия.
#
# Собирается ПОВЕРХ опубликованного образа: PHP-репозиторий при этом не изменяется,
# что прямо требуется CLAUDE.md. Всё, что здесь есть, живёт в этом репозитории.
ARG BASE_IMAGE=ghcr.io/joinrpg/kogda-igra:latest
FROM ${BASE_IMAGE}

RUN pecl install xdebug && docker-php-ext-enable xdebug

# Сбор покрытия включается на каждый запрос через auto_prepend_file.
COPY collect.php /coverage-collect.php

RUN { \
      echo 'xdebug.mode=coverage'; \
      echo 'auto_prepend_file=/coverage-collect.php'; \
    } > /usr/local/etc/php/conf.d/zz-coverage.ini

# Сюда складываются сырые дампы — по файлу на запрос.
RUN mkdir -p /coverage-data && chmod 777 /coverage-data
